using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace Transform.Test
{
    internal class SMEVExecJava
    {
        string _SMEVFolderPath = String.Empty;
        string _SMEVTempPath = @"C:\Temp";
        string _SMEVFileName = "SmevTransformSpi.jar";

        internal string SMEVFileNameFull { get { return _SMEVFolderPath + "\\" + _SMEVFileName; } }

        public void SMEVTransform(MemoryStream inXml, MemoryStream outXml)
        {
            _SMEVFolderPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!File.Exists(SMEVFileNameFull))
                throw new Exception("Ошибка.Не найден файл SmevTransformSpi.jar");

            System.Guid guid = System.Guid.NewGuid();
            string srcPath = Path.Combine(_SMEVTempPath, guid.ToString() + ".xml");
            string dstPath = Path.Combine(_SMEVTempPath, guid.ToString() + "_result.xml");
            using (FileStream file = new FileStream(srcPath, FileMode.Create, FileAccess.Write))
            {
                inXml.CopyTo(file);
            }
            string transformResult = string.Empty;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd", @"/c java -jar " + SMEVFileNameFull + " " + srcPath + " " + dstPath);
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
                for (int i = 0; i < 20; i++)
                {
                    if (File.Exists(dstPath))
                    {
                        Thread.Sleep(200);
                        break;
                    }
                    Thread.Sleep(200);
                }
                using (FileStream sr = new FileStream(dstPath, FileMode.Open, FileAccess.Read))
                {
                    sr.CopyTo(outXml);
                    outXml.Position = 0;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                File.Delete(srcPath);
                File.Delete(dstPath);
            }
        }

        public string doTransform(string xml)
        {
            using (MemoryStream inStream = new MemoryStream())
            {
                var writer = new StreamWriter(inStream);
                writer.Write(xml);
                writer.Flush();
                inStream.Position = 0;

                using (MemoryStream outStream = new MemoryStream())
                {
                    SMEVTransform(inStream, outStream);

                    var reader = new StreamReader(outStream);
                    return reader.ReadToEnd();
                }
            }
        }
    }
}

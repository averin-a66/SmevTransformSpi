using Transform.Test.Properties;
using NUnit.Framework;
using System.IO;
using System.Text;
using System.Xml;
using GostCryptography.Xml.Smev;

namespace Transform.Test.Tests.TestFromMetodichka
{
    public class SmevTransformSpiTests
    {
        [SetUp]
        public void Setup()
        {
        }

        // 1.	�������� ��������� XML declaration � processing instructions(��� �������)
        [Test]
        public void Test2()
        {
            execTestTransfornXml(Resources.Test2);
        }

        // 2.	�������� ��������� ������ (��������� XML), ���������� ������ ���������� �������. ���� ��������� ���� �������� ������
        // ���������� ������� � \u0009 - ���������, \u000A � ������� ������ (Unix), \u000D - ������� �������, \u0020, � ����� ����� ������ ������� � ����� ������� \u0020,
        // �� ��� ���������� ������� ����������. � ���������� ������� �������������� ����������� ���������� ���������� �������� ����� ������ XML
        [Test]
        public void Test3()
        {
            execTestTransfornXml(Resources.Test3);
        }

        // 3.	����� ���������� ������ �� ������� 1 � 2, ���� ���� � �������� ��� �������� �����, ������� �� ����� ���� ����������� � ���� empty element tag
        // (http://www.w3.org/TR/2008/REC-xml-20081126/#sec-starttags, ������� [44]),  ����������� �������������� �������� � ���� start-tag + end-tag.
        [Test]
        public void Test4()
        {
            execTestTransfornXml(Resources.Test4);
        }

        // 4.	�������� namespace prefix, ������� �� ������� ������ �����������, �� �� ������������ � ���������� �����.
        [Test]
        public void Test5()
        {
            execTestTransfornXml(Resources.Test5);
        }

        // 5.	�������� ������� ���������� namespace �������� �������� ���� � ������� ��������, ���� ���� �� ������.
        // ���� ���������� namespase �����������, �� ���������� namespace ����������� � ������� ��������.
        [Test]
        public void Test6()
        {
            execTestTransfornXml(Resources.Test6);
        }

        // 6.	Namespace prefix ��������� � ��������� ������ ���� �������� �� ������������� ���������������. ��������������� ������� ������� �� �������� �ns�,
        // � ����������� ������ ���������������� �������� � ������ ��������������� XML-���������, ������� � �������. ��� ��������� ��������� ������ ����������� �� ������������
        [Test]
        public void Test7()
        {
            execTestTransfornXml(Resources.Test7);
        }

        // 7.	�������� ������ ���� ������������� � ���������� �������: ������� �� namespace URI (���� ������� - � qualified form), ����� � �� local name.
        //      �������� � unqualified form ����� ���������� ���� ����� ��������� � qualified form.
        // 8.	���������� namespace prefix ������ ���������� ����� ����������.���������� ��������� ������ ���� ������������� � ������� ����������, � ������:
        //      a.������ ����������� ������� ������������ ��� ��������, ���� �� �� ��� �������� ���� �� ������.
        //      b.������ ����������� �������� ����������� ��� ���������, ���� ��� ���������.������� ���� ���������� ������������� ������� ���������,
        //        ��������������� � ���������� ������� (��.�.7 �������� �������).
        [Test]
        public void Test8()
        {
            execTestTransfornXml(Resources.Test8);
        }

        public static void execTestTransfornXml(string xml)
        {
            string resultJava = ExecTransformJava(xml);
            string result = ExecTransform(xml);

            Assert.AreEqual(resultJava, result);
        }

        public static string ExecTransform(string xml)
        {
            MemoryStream src = new MemoryStream(Encoding.UTF8.GetBytes(xml));
            src.Position = 0;
            MemoryStream dst = new MemoryStream();

            SmevTransformSpi tramsform = new SmevTransformSpi();
            tramsform.process(src, dst);
            byte[] data = dst.ToArray();
            string BOMMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());
            string  p = Encoding.UTF8.GetString(data);
            if (p.StartsWith(BOMMarkUtf8))
                p = p.Remove(0, BOMMarkUtf8.Length);
            return p;
        }

        public static string ExecTransformJava(string xml)
        {
            SMEVExecJava trnJava = new SMEVExecJava();
            return trnJava.doTransform(xml);
        }

        private static XmlDocument CreateXmlDocument()
        {
            var document = new XmlDocument();
            document.LoadXml(Resources.OKPD2TypeRequest);
            return document;
        }
    }
}
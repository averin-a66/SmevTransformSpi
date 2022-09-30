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

        // 1.	Удаления элементов XML declaration и processing instructions(при наличии)
        [Test]
        public void Test2()
        {
            execTestTransfornXml(Resources.Test2);
        }

        // 2.	Удаление текстовых блоков (элементов XML), содержащих только пробельные символы. Если текстовый блок содержит только
        // пробельные символы – \u0009 - табуляция, \u000A – перевод строки (Unix), \u000D - возврат каретки, \u0020, а также любые другие символы с кодом меньшим \u0020,
        // то все пробельные символы вырезаются. В результате данного преобразования достигается отсутствие пробельных символов между тэгами XML
        [Test]
        public void Test3()
        {
            execTestTransfornXml(Resources.Test3);
        }

        // 3.	После применения правил из пунктов 1 и 2, если даже у элемента нет дочерних узлов, элемент не может быть представлен в виде empty element tag
        // (http://www.w3.org/TR/2008/REC-xml-20081126/#sec-starttags, правило [44]),  выполняется преобразование элемента в пару start-tag + end-tag.
        [Test]
        public void Test4()
        {
            execTestTransfornXml(Resources.Test4);
        }

        // 4.	Удаление namespace prefix, которые на текущем уровне объявляются, но не используются в низлежащих тегах.
        [Test]
        public void Test5()
        {
            execTestTransfornXml(Resources.Test5);
        }

        // 5.	Проверка наличия объявления namespace текущего элемента либо в текущем элементе, либо выше по дереву.
        // Если объявления namespase отсутствует, то объявление namespace выполняется в текущем элементе.
        [Test]
        public void Test6()
        {
            execTestTransfornXml(Resources.Test6);
        }

        // 6.	Namespace prefix элементов и атрибутов должны быть заменены на автоматически сгенерированные. Сгенерированный префикс состоит из литерала «ns»,
        // и порядкового номера сгенерированного префикса в рамках обрабатываемого XML-фрагмента, начиная с единицы. При генерации префиксов должно устраняться их дублирование
        [Test]
        public void Test7()
        {
            execTestTransfornXml(Resources.Test7);
        }

        // 7.	Атрибуты должны быть отсортированы в алфавитном порядке: сначала по namespace URI (если атрибут - в qualified form), затем – по local name.
        //      Атрибуты в unqualified form после сортировки идут после атрибутов в qualified form.
        // 8.	Объявления namespace prefix должны находиться перед атрибутами.Объявления префиксов должны быть отсортированы в порядке объявления, а именно:
        //      a.Первым объявляется префикс пространства имён элемента, если он не был объявлен выше по дереву.
        //      b.Дальше объявляются префиксы пространств имён атрибутов, если они требуются.Порядок этих объявлений соответствует порядку атрибутов,
        //        отсортированных в алфавитном порядке (см.п.7 текущего перечня).
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
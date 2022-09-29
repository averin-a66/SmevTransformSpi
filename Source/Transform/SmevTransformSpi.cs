using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;

namespace GostCryptography.Xml.SmevTransformSpi
{
    public class SmevTransformSpi
    {
        const string nsDefault = "http://www.w3.org/2000/xmlns/";

        public void process(MemoryStream argSrc, MemoryStream argDst) 
        {
            Stack<List<Namespace>> prefixMappingStack = new Stack<List<Namespace>>();
            using (XmlReader src = XmlReader.Create(argSrc))
            //using (XmlWriter dst = XmlWriter.Create(argDst))
            using (XmlSmevWriter dst = new XmlSmevWriter(argDst, Encoding.UTF8))
            {
                int prefixCnt = 1;

                try
                {
                    while (src.Read())
                    {
                        if (src.NodeType == XmlNodeType.Text)
                        {
                            string data = src.Value;
                            // Отсекаем пустые символы.
                            if (!string.IsNullOrEmpty(data.Trim()))
                            {
                                dst.WriteString(data);
                            }
                        }
                        else if (src.NodeType == XmlNodeType.Element)
                        {
                            bool IsElementEmpty = src.IsEmptyElement;

                            List<Namespace> myPrefixMappings = new List<Namespace>();
                            prefixMappingStack.Push(myPrefixMappings);

                            // Обработка элемента: NS prefix rewriting.
                            // N.B. Элементы в unqualified form не поддерживаются.
                            //StartElement srcEvent = (StartElement) event;
                            string nsURI = src.NamespaceURI;
                            string prefix = findPrefix(nsURI, prefixMappingStack);
                            if (string.IsNullOrEmpty(prefix))
                            {
                                prefix = "ns" + (prefixCnt++).ToString();
                                myPrefixMappings.Add(new Namespace(prefix, nsURI));
                            }
                            dst.WriteStartElement(prefix, src.LocalName, nsURI);

                            // == Обработка атрибутов. Два шага: отсортировать, промэпить namespace URI. ==
                            // Положим атрибуты в list, чтобы их можно было отсортировать.
                            List<Attribute> srcAttributeList = GetAttributes(src);
                            // Сортировка атрибутов по алфавиту.
                            srcAttributeList.Sort(new AttributeSortingComparator());

                            // Обработка префиксов. Аналогична обработке префиксов элементов,
                            // за исключением того, что у атрибут может не иметь namespace.
                            List<Attribute> dstAttributeList = new List<Attribute>();

                            foreach (Attribute srcAttribute in srcAttributeList)
                            {
                                string attributeNsURI = srcAttribute.Namespace.NamespaceURI;
                                string attributeLocalName = srcAttribute.Name;
                                string value = srcAttribute.Value;
                                Attribute dstAttribute;
                                if (!string.IsNullOrEmpty(attributeNsURI))
                                {
                                    string attributePrefix = findPrefix(attributeNsURI, prefixMappingStack);
                                    if (attributePrefix == null)
                                    {
                                        attributePrefix = "ns" + prefixCnt++;
                                        myPrefixMappings.Add(new Namespace(attributePrefix, attributeNsURI));
                                    }
                                    dstAttribute = new Attribute(attributeLocalName, value, new Namespace(attributePrefix, attributeNsURI));
                                }
                                else
                                {
                                    dstAttribute = new Attribute(attributeLocalName, value);
                                }
                                dstAttributeList.Add(dstAttribute);

                            }

                            // Вывести namespace prefix mappings для текущего элемента.
                            // Их порядок детерминирован, т.к. перед мэппингом атрибуты были отсортированы.
                            // Поэтому дополнительной сотрировки здесь не нужно.
                            dst.WriteNamespaces();
                            /*foreach (Namespace mapping in myPrefixMappings)
                            {
                                dst.WriteN("xmlns:" + mapping.Prefix, mapping.NamespaceURI);
                            }*/

                            // Вывести атрибуты.
                            // N.B. Мы не выводим атрибуты сразу вместе с элементом, используя метод
                            // XMLEventFactory.createStartElement(prefix, nsURI, localName, List<Namespace>, List<Attribute>),
                            // потому что при использовании этого метода порядок атрибутов в выходном документе
                            // меняется произвольным образом.
                            foreach(Attribute attr in dstAttributeList)
                            {
                                dst.WriteAttributeString(attr.Namespace?.Prefix, attr.Name, attr.Namespace?.NamespaceURI, attr.Value);
                            }

                            if(IsElementEmpty)
                            {
                                // Гарантируем, что empty tags запишутся в форме <a></a>, а не в форме <a/>.
                                dst.WriteFullEndElement();
                                prefixMappingStack.Pop();
                            }

                        }
                        else if (src.NodeType == XmlNodeType.EndElement) 
                        {
                            dst.WriteFullEndElement();

                            prefixMappingStack.Pop();
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }

            string findPrefix(String argNamespaceURI, Stack<List<Namespace>> argMappingStack)
            {
                if (argNamespaceURI == null)
                {
                    throw new ArgumentException("No namespace элементы не поддерживаются.");
                }

                foreach(List<Namespace> elementMappingList in argMappingStack) 
                {
                    foreach (Namespace mapping in elementMappingList)
                    {
                        if (argNamespaceURI == mapping.NamespaceURI)
                        {
                            return mapping.Prefix;
                        }
                    }
                }
                return null;
            }


            List<Attribute> GetAttributes(XmlReader src)
            {
                List<Attribute> srcAttributeList = new List<Attribute>();
                for (int i = 0; i < src.AttributeCount; i++)
                {
                    src.MoveToAttribute(i);
                    if (src.Name == "xmlns" || (src.Name.Length > 6 && src.Name.Substring(0, 6) == "xmlns:"))
                        continue;
                    srcAttributeList.Add(new Attribute(src.LocalName, src.Value, new Namespace("", src.NamespaceURI == nsDefault ? null : src.NamespaceURI)));
                }

                return srcAttributeList;
            }
        }

        private class AttributeSortingComparator : IComparer<Attribute>
        {
            public int Compare(Attribute x, Attribute y)
            {
                string xNS = x.Namespace.NamespaceURI;
                string xLocal = x.Name;
                string yNS = y.Namespace.NamespaceURI;
                string yLocal = y.Name;

                // Оба атрибута - unqualified.
                if (empty(xNS) && empty(yNS))
                {
                    return xLocal.CompareTo(yLocal);
                }

                // Оба атрибута - qualified.
                if (!empty(xNS) && !empty(yNS))
                {
                    // Сначала сравниваем namespaces.
                    int nsComparisonResult = xNS.CompareTo(yNS);
                    if (nsComparisonResult != 0)
                    {
                        return nsComparisonResult;
                    }
                    else
                    {
                        // Если равны - local names.
                        return xLocal.CompareTo(yLocal);
                    }
                }

                // Один - qualified, второй - unqualified.
                if (empty(xNS))
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }

            private bool empty(String arg)
            {
                return string.IsNullOrEmpty(arg);
            }
        }
    

        class Namespace
        {   
            public string Prefix { get; protected set; }
            public string NamespaceURI { get; protected set; }

            public Namespace(string prefix, string namespaceURI)
            {
                Prefix = prefix;
                NamespaceURI = namespaceURI;
            }
        }

        class Attribute
        {
            public Namespace Namespace { get; protected set; }
            public string Name { get; protected set; }
            public string Value { get; protected set; }

            public Attribute(string name, string value, Namespace nspace=null)
            {
                Name = name;
                Value = value;
                Namespace = nspace;
            }
        }
    }
}

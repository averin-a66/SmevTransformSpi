using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GostCryptography.Xml.Smev
{
    internal class XmlTextEncoder
    {
        //
        // Fields
        //
        // output text writer
        TextWriter textWriter;

        // true when writing out the content of attribute value
        bool inAttribute;

        // quote char of the attribute (when inAttribute) 
        char quoteChar;

        // caching of attribute value
        StringBuilder attrValue;
        bool cacheAttrValue;

        // XmlCharType
        XmlCharType xmlCharType;

        //
        // Constructor
        //
        internal XmlTextEncoder(TextWriter textWriter)
        {
            this.textWriter = textWriter;
            this.quoteChar = '"';
            this.xmlCharType = XmlCharType.Instance;
        }

        //
        // Internal methods and properties
        //
        internal char QuoteChar
        {
            set
            {
                this.quoteChar = value;
            }
        }

        internal void StartAttribute(bool cacheAttrValue)
        {
            this.inAttribute = true;
            this.cacheAttrValue = cacheAttrValue;
            if (cacheAttrValue)
            {
                if (attrValue == null)
                {
                    attrValue = new StringBuilder();
                }
                else
                {
                    attrValue.Length = 0;
                }
            }
        }

        internal void EndAttribute()
        {
            if (cacheAttrValue)
            {
                attrValue.Length = 0;
            }
            this.inAttribute = false;
            this.cacheAttrValue = false;
        }

        internal string AttributeValue
        {
            get
            {
                if (cacheAttrValue)
                {
                    return attrValue.ToString();
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        internal void WriteSurrogateChar(char lowChar, char highChar)
        {
            if (!XmlCharType.IsLowSurrogate(lowChar) ||
                 !XmlCharType.IsHighSurrogate(highChar))
            {
                throw new Exception("XmlConvert.CreateInvalidSurrogatePairException(lowChar, highChar)");
            }

            textWriter.Write(highChar);
            textWriter.Write(lowChar);
        }

        internal void Write(char[] array, int offset, int count)
        {
            if (null == array)
            {
                throw new ArgumentNullException("array");
            }

            if (0 > offset)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (0 > count)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (count > array.Length - offset)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (cacheAttrValue)
            {
                attrValue.Append(array, offset, count);
            }

            int endPos = offset + count;
            int i = offset;
            char ch = (char)0;
            for (; ; )
            {
                int startPos = i;
                //unsafe
                {
                    while (i < endPos && (xmlCharType.charProperties[ch = array[i]] & XmlCharType.fAttrValue) != 0)
                    { // ( xmlCharType.IsAttributeValueChar( ( ch = array[i] ) ) ) ) {
                        i++;
                    }
                }

                if (startPos < i)
                {
                    textWriter.Write(array, startPos, i - startPos);
                }
                if (i == endPos)
                {
                    break;
                }

                switch (ch)
                {
                    case (char)0x9:
                        textWriter.Write(ch);
                        break;
                    case (char)0xA:
                    case (char)0xD:
                        if (inAttribute)
                        {
                            WriteCharEntityImpl(ch);
                        }
                        else
                        {
                            textWriter.Write(ch);
                        }
                        break;

                    case '<':
                        WriteEntityRefImpl("lt");
                        break;
                    case '>':
                        WriteEntityRefImpl("gt");
                        break;
                    case '&':
                        WriteEntityRefImpl("amp");
                        break;
                    case '\'':
                        if (inAttribute && quoteChar == ch)
                        {
                            WriteEntityRefImpl("apos");
                        }
                        else
                        {
                            textWriter.Write('\'');
                        }
                        break;
                    case '"':
                        if (inAttribute && quoteChar == ch)
                        {
                            WriteEntityRefImpl("quot");
                        }
                        else
                        {
                            textWriter.Write('"');
                        }
                        break;
                    default:
                        if (XmlCharType.IsHighSurrogate(ch))
                        {
                            if (i + 1 < endPos)
                            {
                                WriteSurrogateChar(array[++i], ch);
                            }
                            else
                            {
                                throw new ArgumentException("The second character surrogate pair is not in the input buffer to be written.");
                            }
                        }
                        else if (XmlCharType.IsLowSurrogate(ch))
                        {
                            throw new Exception("XmlConvert.CreateInvalidHighSurrogateCharException(ch)");
                        }
                        else
                        {
                            Debug.Assert((ch < 0x20 && !xmlCharType.IsWhiteSpace(ch)) || (ch > 0xFFFD));
                            WriteCharEntityImpl(ch);
                        }
                        break;
                }
                i++;
            }
        }

        internal void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            if (!XmlCharType.IsLowSurrogate(lowChar) ||
                 !XmlCharType.IsHighSurrogate(highChar))
            {
                throw new Exception("XmlConvert.CreateInvalidSurrogatePairException(lowChar, highChar)");

            }
            int surrogateChar = XmlCharType.CombineSurrogateChar(lowChar, highChar);

            if (cacheAttrValue)
            {
                attrValue.Append(highChar);
                attrValue.Append(lowChar);
            }

            textWriter.Write("&#x");
            textWriter.Write(surrogateChar.ToString("X", NumberFormatInfo.InvariantInfo));
            textWriter.Write(';');
        }

        internal void Write(string text)
        {
            if (text == null)
            {
                return;
            }

            if (cacheAttrValue)
            {
                attrValue.Append(text);
            }

            // scan through the string to see if there are any characters to be escaped
            int len = text.Length;
            int i = 0;
            int startPos = 0;
            char ch = (char)0;
            for (; ; )
            {
                //unsafe
                {
                    while (i < len && (xmlCharType.charProperties[ch = text[i]] & XmlCharType.fAttrValue) != 0)
                    { // ( xmlCharType.IsAttributeValueChar( ( ch = text[i] ) ) ) ) {
                        i++;
                    }
                }
                if (i == len)
                {
                    // reached the end of the string -> write it whole out
                    textWriter.Write(text);
                    return;
                }
                if (inAttribute)
                {
                    if (ch == 0x9)
                    {
                        i++;
                        continue;
                    }
                }
                else
                {
                    if (ch == 0x9 || ch == 0xA || ch == 0xD || ch == '"' || ch == '\'')
                    {
                        i++;
                        continue;
                    }
                }
                // some character that needs to be escaped is found:
                break;
            }

            char[] helperBuffer = new char[256];
            for (; ; )
            {
                if (startPos < i)
                {
                    WriteStringFragment(text, startPos, i - startPos, helperBuffer);
                }
                if (i == len)
                {
                    break;
                }

                switch (ch)
                {
                    case (char)0x9:
                        textWriter.Write(ch);
                        break;
                    case (char)0xA:
                    case (char)0xD:
                        if (inAttribute)
                        {
                            WriteCharEntityImpl(ch);
                        }
                        else
                        {
                            textWriter.Write(ch);
                        }
                        break;
                    case '<':
                        WriteEntityRefImpl("lt");
                        break;
                    case '>':
                        WriteEntityRefImpl("gt");
                        break;
                    case '&':
                        WriteEntityRefImpl("amp");
                        break;
                    case '\'':
                        if (inAttribute && quoteChar == ch)
                        {
                            WriteEntityRefImpl("apos");
                        }
                        else
                        {
                            textWriter.Write('\'');
                        }
                        break;
                    case '"':
                        if (inAttribute && quoteChar == ch)
                        {
                            WriteEntityRefImpl("quot");
                        }
                        else
                        {
                            textWriter.Write('"');
                        }
                        break;
                    default:
                        if (XmlCharType.IsHighSurrogate(ch))
                        {
                            if (i + 1 < len)
                            {
                                WriteSurrogateChar(text[++i], ch);
                            }
                            else
                            {
                                throw new Exception("XmlConvert.CreateInvalidSurrogatePairException(text[i], ch)");
                            }
                        }
                        else if (XmlCharType.IsLowSurrogate(ch))
                        {
                            throw new Exception("XmlConvert.CreateInvalidHighSurrogateCharException(ch)");
                        }
                        else
                        {
                            Debug.Assert((ch < 0x20 && !xmlCharType.IsWhiteSpace(ch)) || (ch > 0xFFFD));
                            WriteCharEntityImpl(ch);
                        }
                        break;
                }
                i++;
                startPos = i;
                //unsafe
                {
                    while (i < len && (xmlCharType.charProperties[ch = text[i]] & XmlCharType.fAttrValue) != 0)
                    { // ( xmlCharType.IsAttributeValueChar( ( text[i] ) ) ) ) {
                        i++;
                    }
                }
            }
        }

        internal void WriteRawWithSurrogateChecking(string text)
        {
            if (text == null)
            {
                return;
            }
            if (cacheAttrValue)
            {
                attrValue.Append(text);
            }

            int len = text.Length;
            int i = 0;
            char ch = (char)0;

            for (; ; )
            {
                //unsafe
                {
                    while (i < len &&
                        ((xmlCharType.charProperties[ch = text[i]] & XmlCharType.fCharData) != 0 // ( xmlCharType.IsCharData( ( ch = text[i] ) ) 
                        || ch < 0x20))
                    {
                        i++;
                    }
                }
                if (i == len)
                {
                    break;
                }
                if (XmlCharType.IsHighSurrogate(ch))
                {
                    if (i + 1 < len)
                    {
                        char lowChar = text[i + 1];
                        if (XmlCharType.IsLowSurrogate(lowChar))
                        {
                            i += 2;
                            continue;
                        }
                        else
                        {
                            throw new Exception("XmlConvert.CreateInvalidSurrogatePairException(lowChar, ch)");
                        }
                    }
                    throw new ArgumentException("The surrogate pair is invalid. Missing a low surrogate character.");
                }
                else if (XmlCharType.IsLowSurrogate(ch))
                {
                    throw new Exception("XmlConvert.CreateInvalidHighSurrogateCharException(ch)");
                }
                else
                {
                    i++;
                }
            }

            textWriter.Write(text);
            return;
        }

        internal void WriteRaw(string value)
        {
            if (cacheAttrValue)
            {
                attrValue.Append(value);
            }
            textWriter.Write(value);
        }

        internal void WriteRaw(char[] array, int offset, int count)
        {
            if (null == array)
            {
                throw new ArgumentNullException("array");
            }

            if (0 > count)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (0 > offset)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (count > array.Length - offset)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            if (cacheAttrValue)
            {
                attrValue.Append(array, offset, count);
            }
            textWriter.Write(array, offset, count);
        }



        internal void WriteCharEntity(char ch)
        {
            if (XmlCharType.IsSurrogate(ch))
            {
                throw new ArgumentException("The surrogate pair is invalid. Missing a low surrogate character.");
            }

            string strVal = ((int)ch).ToString("X", NumberFormatInfo.InvariantInfo);
            if (cacheAttrValue)
            {
                attrValue.Append("&#x");
                attrValue.Append(strVal);
                attrValue.Append(';');
            }
            WriteCharEntityImpl(strVal);
        }

        internal void WriteEntityRef(string name)
        {
            if (cacheAttrValue)
            {
                attrValue.Append('&');
                attrValue.Append(name);
                attrValue.Append(';');
            }
            WriteEntityRefImpl(name);
        }

        internal void Flush()
        {
            // 
        }

        //
        // Private implementation methods
        //
        // This is a helper method to woraround the fact that TextWriter does not have a Write method 
        // for fragment of a string such as Write( string, offset, count). 
        // The string fragment will be written out by copying into a small helper buffer and then 
        // calling textWriter to write out the buffer.
        private void WriteStringFragment(string str, int offset, int count, char[] helperBuffer)
        {
            int bufferSize = helperBuffer.Length;
            while (count > 0)
            {
                int copyCount = count;
                if (copyCount > bufferSize)
                {
                    copyCount = bufferSize;
                }

                str.CopyTo(offset, helperBuffer, 0, copyCount);
                textWriter.Write(helperBuffer, 0, copyCount);
                offset += copyCount;
                count -= copyCount;
            }
        }

        private void WriteCharEntityImpl(char ch)
        {
            WriteCharEntityImpl(((int)ch).ToString("X", NumberFormatInfo.InvariantInfo));
        }

        private void WriteCharEntityImpl(string strVal)
        {
            textWriter.Write("&#x");
            textWriter.Write(strVal);
            textWriter.Write(';');
        }

        private void WriteEntityRefImpl(string name)
        {
            textWriter.Write('&');
            textWriter.Write(name);
            textWriter.Write(';');
        }
    }
}

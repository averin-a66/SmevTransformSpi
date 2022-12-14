using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Reflection;
using System.Security;



namespace GostCryptography.Xml.Smev
{
    internal static class XmlReservedNs
    {
        internal const string NsXml = "http://www.w3.org/XML/1998/namespace";
        internal const string NsXmlNs = "http://www.w3.org/2000/xmlns/";
    };

    public enum XmlSpace
    {
        // xml:space scope has not been specified.
        None = 0,

        // The xml:space scope is "default".
        Default = 1,

        // The xml:space scope is "preserve".
        Preserve = 2
    }

    // Specifies formatting options for XmlTextWriter.
    public enum Formatting
    {
        // No special formatting is done (this is the default).
        None,

        //This option causes child elements to be indented using the Indentation and IndentChar properties.  
        // It only indents Element Content (http://www.w3.org/TR/1998/REC-xml-19980210#sec-element-content)
        // and not Mixed Content (http://www.w3.org/TR/1998/REC-xml-19980210#sec-mixed-content)
        // according to the XML 1.0 definitions of these terms.
        Indented,
    };

    public class XmlSmevWriter : XmlWriter
    {

        internal const string MethodNotUse = "Method not use";
        //
        // Private types
        //
        enum NamespaceState
        {
            Uninitialized,
            NotDeclaredButInScope,
            DeclaredButNotWrittenOut,
            DeclaredAndWrittenOut
        }

        struct TagInfo
        {
            internal string name;
            internal string prefix;
            internal string defaultNs;
            internal NamespaceState defaultNsState;
            internal XmlSpace xmlSpace;
            internal string xmlLang;
            internal int prevNsTop;
            internal int prefixCount;
            internal bool mixed; // whether to pretty print the contents of this element.

            internal void Init(int nsTop)
            {
                name = null;
                defaultNs = String.Empty;
                defaultNsState = NamespaceState.Uninitialized;
                xmlSpace = XmlSpace.None;
                xmlLang = null;
                prevNsTop = nsTop;
                prefixCount = 0;
                mixed = false;
            }
        }

        struct Namespace
        {
            internal string prefix;
            internal string ns;
            internal bool declared;
            internal int prevNsIndex;

            internal void Set(string prefix, string ns, bool declared)
            {
                this.prefix = prefix;
                this.ns = ns;
                this.declared = declared;
                this.prevNsIndex = -1;
            }
        }

        enum SpecialAttr
        {
            None,
            XmlSpace,
            XmlLang,
            XmlNs
        };

        // State machine is working through autocomplete
        private enum State
        {
            Start,
            Prolog,
            PostDTD,
            Element,
            Attribute,
            Content,
            AttrOnly,
            Epilog,
            Error,
            Closed,
        }

        private enum Token
        {
            PI,
            Doctype,
            Comment,
            CData,
            StartElement,
            EndElement,
            LongEndElement,
            StartAttribute,
            EndAttribute,
            Content,
            Base64,
            RawData,
            Whitespace,
            Empty
        }

        //
        // Fields
        //
        // output
        TextWriter textWriter;
        XmlTextEncoder xmlEncoder;
        Encoding encoding;

        // formatting
        Formatting formatting;
        bool indented; // perf - faster to check a boolean.
        int indentation;
        char indentChar;

        // element stack
        TagInfo[] stack;
        int top;

        // state machine for AutoComplete
        State[] stateTable;
        State currentState;
        Token lastToken;

        // Base64 content
        //XmlTextWriterBase64Encoder base64Encoder;

        // misc
        char quoteChar;
        char curQuoteChar;
        bool namespaces;
        SpecialAttr specialAttr;
        string prefixForXmlNs;
        bool flush;

        // namespaces
        Namespace[] nsStack;
        int nsTop;
        Dictionary<string, int> nsHashtable;
        bool useNsHashtable;

        // char types
        XmlCharType xmlCharType = XmlCharType.Instance;

        //
        // Constants and constant tables
        //
        const int NamespaceStackInitialSize = 8;
        const int MaxNamespacesWalkCount = 3;

        static readonly State[] stateTableDefault = {
            //                          State.Start      State.Prolog     State.PostDTD    State.Element    State.Attribute  State.Content   State.AttrOnly   State.Epilog
            //
            /* Token.PI             */ State.Prolog,    State.Prolog,    State.PostDTD,   State.Content,   State.Content,   State.Content,  State.Error,     State.Epilog,
            /* Token.Doctype        */ State.PostDTD,   State.PostDTD,   State.Error,     State.Error,     State.Error,     State.Error,    State.Error,     State.Error,
            /* Token.Comment        */ State.Prolog,    State.Prolog,    State.PostDTD,   State.Content,   State.Content,   State.Content,  State.Error,     State.Epilog,
            /* Token.CData          */ State.Content,   State.Content,   State.Error,     State.Content,   State.Content,   State.Content,  State.Error,     State.Epilog,
            /* Token.StartElement   */ State.Element,   State.Element,   State.Element,   State.Element,   State.Element,   State.Element,  State.Error,     State.Element,
            /* Token.EndElement     */ State.Error,     State.Error,     State.Error,     State.Content,   State.Content,   State.Content,  State.Error,     State.Error,
            /* Token.LongEndElement */ State.Error,     State.Error,     State.Error,     State.Content,   State.Content,   State.Content,  State.Error,     State.Error,
            /* Token.StartAttribute */ State.AttrOnly,  State.Error,     State.Error,     State.Attribute, State.Attribute, State.Error,    State.Error,     State.Error,
            /* Token.EndAttribute   */ State.Error,     State.Error,     State.Error,     State.Error,     State.Element,   State.Error,    State.Epilog,     State.Error,
            /* Token.Content        */ State.Content,   State.Content,   State.Error,     State.Content,   State.Attribute, State.Content,  State.Attribute, State.Epilog,
            /* Token.Base64         */ State.Content,   State.Content,   State.Error,     State.Content,   State.Attribute, State.Content,  State.Attribute, State.Epilog,
            /* Token.RawData        */ State.Prolog,    State.Prolog,    State.PostDTD,   State.Content,   State.Attribute, State.Content,  State.Attribute, State.Epilog,
            /* Token.Whitespace     */ State.Prolog,    State.Prolog,    State.PostDTD,   State.Content,   State.Attribute, State.Content,  State.Attribute, State.Epilog,
        };

        static readonly State[] stateTableDocument = {
            //                          State.Start      State.Prolog     State.PostDTD    State.Element    State.Attribute  State.Content   State.AttrOnly   State.Epilog
            //
            /* Token.PI             */ State.Error,     State.Prolog,    State.PostDTD,   State.Content,   State.Content,   State.Content,  State.Error,     State.Epilog,
            /* Token.Doctype        */ State.Error,     State.PostDTD,   State.Error,     State.Error,     State.Error,     State.Error,    State.Error,     State.Error,
            /* Token.Comment        */ State.Error,     State.Prolog,    State.PostDTD,   State.Content,   State.Content,   State.Content,  State.Error,     State.Epilog,
            /* Token.CData          */ State.Error,     State.Error,     State.Error,     State.Content,   State.Content,   State.Content,  State.Error,     State.Error,
            /* Token.StartElement   */ State.Error,     State.Element,   State.Element,   State.Element,   State.Element,   State.Element,  State.Error,     State.Error,
            /* Token.EndElement     */ State.Error,     State.Error,     State.Error,     State.Content,   State.Content,   State.Content,  State.Error,     State.Error,
            /* Token.LongEndElement */ State.Error,     State.Error,     State.Error,     State.Content,   State.Content,   State.Content,  State.Error,     State.Error,
            /* Token.StartAttribute */ State.Error,     State.Error,     State.Error,     State.Attribute, State.Attribute, State.Error,    State.Error,     State.Error,
            /* Token.EndAttribute   */ State.Error,     State.Error,     State.Error,     State.Error,     State.Element,   State.Error,    State.Error,     State.Error,
            /* Token.Content        */ State.Error,     State.Error,     State.Error,     State.Content,   State.Attribute, State.Content,  State.Error,     State.Error,
            /* Token.Base64         */ State.Error,     State.Error,     State.Error,     State.Content,   State.Attribute, State.Content,  State.Error,     State.Error,
            /* Token.RawData        */ State.Error,     State.Prolog,    State.PostDTD,   State.Content,   State.Attribute, State.Content,  State.Error,     State.Epilog,
            /* Token.Whitespace     */ State.Error,     State.Prolog,    State.PostDTD,   State.Content,   State.Attribute, State.Content,  State.Error,     State.Epilog,
        };

        //
        // Constructors
        //
        internal XmlSmevWriter()
        {
            namespaces = true;
            formatting = Formatting.None;
            indentation = 2;
            indentChar = ' ';
            // namespaces
            nsStack = new Namespace[NamespaceStackInitialSize];
            nsTop = -1;
            // element stack
            stack = new TagInfo[10];
            top = 0;// 0 is an empty sentanial element
            stack[top].Init(-1);
            quoteChar = '"';

            stateTable = stateTableDefault;
            currentState = State.Start;
            lastToken = Token.Empty;
        }

        // Creates an instance of the XmlTextWriter class using the specified stream.
        public XmlSmevWriter(Stream w, Encoding encoding) : this()
        {
            this.encoding = encoding;
            if (encoding != null)
                textWriter = new StreamWriter(w, encoding);
            else
                textWriter = new StreamWriter(w);
            xmlEncoder = new XmlTextEncoder(textWriter);
            xmlEncoder.QuoteChar = this.quoteChar;
        }

        void doThrowMethodNotUse()
        {
            doThrowMethodNotUse();
        }

        //
        // XmlWriter implementation
        //
        // Writes out the XML declaration with the version "1.0".
        public override void WriteStartDocument()
        {
            doThrowMethodNotUse();
        }

        // Writes out the XML declaration with the version "1.0" and the standalone attribute.
        public override void WriteStartDocument(bool standalone)
        {
            doThrowMethodNotUse();
        }

        // Closes any open elements or attributes and puts the writer back in the Start state.
        public override void WriteEndDocument()
        {
            doThrowMethodNotUse();
        }

        // Writes out the DOCTYPE declaration with the specified name and optional attributes.
        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
            doThrowMethodNotUse();
        }

        // Writes out the specified start tag and associates it with the given namespace and prefix.
        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            try
            {
                AutoComplete(Token.StartElement);
                PushStack();
                textWriter.Write('<');

                if (this.namespaces)
                {
                    // Propagate default namespace and mix model down the stack.
                    stack[top].defaultNs = stack[top - 1].defaultNs;
                    if (stack[top - 1].defaultNsState != NamespaceState.Uninitialized)
                        stack[top].defaultNsState = NamespaceState.NotDeclaredButInScope;
                    stack[top].mixed = stack[top - 1].mixed;
                    if (ns == null)
                    {
                        // use defined prefix
                        if (prefix != null && prefix.Length != 0 && (LookupNamespace(prefix) == -1))
                        {
                            throw new ArgumentException("An undefined prefix is in use.");
                        }
                    }
                    else
                    {
                        if (prefix == null)
                        {
                            string definedPrefix = FindPrefix(ns);
                            if (definedPrefix != null)
                            {
                                prefix = definedPrefix;
                            }
                            else
                            {
                                PushNamespace(null, ns, false); // new default
                            }
                        }
                        else if (prefix.Length == 0)
                        {
                            PushNamespace(null, ns, false); // new default
                        }
                        else
                        {
                            if (ns.Length == 0)
                            {
                                prefix = null;
                            }
                            VerifyPrefixXml(prefix, ns);
                            PushNamespace(prefix, ns, false); // define
                        }
                    }
                    stack[top].prefix = null;
                    if (prefix != null && prefix.Length != 0)
                    {
                        stack[top].prefix = prefix;
                        textWriter.Write(prefix);
                        textWriter.Write(':');
                    }
                }
                else
                {
                    if ((ns != null && ns.Length != 0) || (prefix != null && prefix.Length != 0))
                    {
                        throw new ArgumentException("NoNamespaces");
                    }
                }
                stack[top].name = localName;
                textWriter.Write(localName);
            }
            catch
            {
                currentState = State.Error;
                throw;
            }
        }

        // Closes one element and pops the corresponding namespace scope.
        public override void WriteEndElement()
        {
            InternalWriteEndElement(false);
        }

        // Closes one element and pops the corresponding namespace scope.
        public override void WriteFullEndElement()
        {
            InternalWriteEndElement(true);
        }

        // Writes the start of an attribute.
        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            try
            {
                AutoComplete(Token.StartAttribute);

                this.specialAttr = SpecialAttr.None;
                if (this.namespaces)
                {

                    if (prefix != null && prefix.Length == 0)
                    {
                        prefix = null;
                    }

                    if (ns == XmlReservedNs.NsXmlNs && prefix == null && localName != "xmlns")
                    {
                        prefix = "xmlns";
                    }

                    if (prefix == "xml")
                    {
                        if (localName == "lang")
                        {
                            this.specialAttr = SpecialAttr.XmlLang;
                        }
                        else if (localName == "space")
                        {
                            this.specialAttr = SpecialAttr.XmlSpace;
                        }
                        /* bug54408. to be fwd compatible we need to treat xml prefix as reserved
                        and not really insist on a specific value. Who knows in the future it
                        might be OK to say xml:blabla
                        else {
                            throw new ArgumentException(Res.GetString(Res.Xml_InvalidPrefix));
                        }*/
                    }
                    else if (prefix == "xmlns")
                    {

                        if (XmlReservedNs.NsXmlNs != ns && ns != null)
                        {
                            throw new ArgumentException("The 'xmlns' attribute is bound to the reserved namespace 'http://www.w3.org/2000/xmlns/'.");
                        }
                        if (localName == null || localName.Length == 0)
                        {
                            localName = prefix;
                            prefix = null;
                            this.prefixForXmlNs = null;
                        }
                        else
                        {
                            this.prefixForXmlNs = localName;
                        }
                        this.specialAttr = SpecialAttr.XmlNs;
                    }
                    else if (prefix == null && localName == "xmlns")
                    {
                        if (XmlReservedNs.NsXmlNs != ns && ns != null)
                        {
                            // add the below line back in when DOM is fixed
                            throw new ArgumentException("The 'xmlns' attribute is bound to the reserved namespace 'http://www.w3.org/2000/xmlns/'.");
                        }
                        this.specialAttr = SpecialAttr.XmlNs;
                        this.prefixForXmlNs = null;
                    }
                    else
                    {
                        if (ns == null)
                        {
                            // use defined prefix
                            if (prefix != null && (LookupNamespace(prefix) == -1))
                            {
                                throw new ArgumentException("An undefined prefix is in use.");
                            }
                        }
                        else if (ns.Length == 0)
                        {
                            // empty namespace require null prefix
                            prefix = string.Empty;
                        }
                        else
                        { // ns.Length != 0
                            VerifyPrefixXml(prefix, ns);
                            if (prefix != null && LookupNamespaceInCurrentScope(prefix) != -1)
                            {
                                prefix = null;
                            }
                            // Now verify prefix validity
                            string definedPrefix = FindPrefix(ns);
                            if (definedPrefix != null && (prefix == null || prefix == definedPrefix))
                            {
                                prefix = definedPrefix;
                            }
                            else
                            {
                                if (prefix == null)
                                {
                                    prefix = GeneratePrefix(); // need a prefix if
                                }
                                PushNamespace(prefix, ns, false);
                            }
                        }
                    }
                    if (prefix != null && prefix.Length != 0)
                    {
                        textWriter.Write(prefix);
                        textWriter.Write(':');
                    }
                }
                else
                {
                    if ((ns != null && ns.Length != 0) || (prefix != null && prefix.Length != 0))
                    {
                        throw new ArgumentException("Cannot set the namespace if Namespaces is 'false'.");
                    }
                    if (localName == "xml:lang")
                    {
                        this.specialAttr = SpecialAttr.XmlLang;
                    }
                    else if (localName == "xml:space")
                    {
                        this.specialAttr = SpecialAttr.XmlSpace;
                    }
                }
                xmlEncoder.StartAttribute(this.specialAttr != SpecialAttr.None);

                textWriter.Write(localName);
                textWriter.Write('=');
                if (this.curQuoteChar != this.quoteChar)
                {
                    this.curQuoteChar = this.quoteChar;
                    xmlEncoder.QuoteChar = this.quoteChar;
                }
                textWriter.Write(this.curQuoteChar);
            }
            catch
            {
                currentState = State.Error;
                throw;
            }
        }

        // Closes the attribute opened by WriteStartAttribute.
        public override void WriteEndAttribute()
        {
            try
            {
                AutoComplete(Token.EndAttribute);
            }
            catch
            {
                currentState = State.Error;
                throw;
            }
        }

        // Writes out a &lt;![CDATA[...]]&gt; block containing the specified text.
        public override void WriteCData(string text)
        {
            doThrowMethodNotUse();
        }

        // Writes out a comment <!--...--> containing the specified text.
        public override void WriteComment(string text)
        {
            doThrowMethodNotUse();
        }

        // Writes out a processing instruction with a space between the name and text as follows: <?name text?>
        public override void WriteProcessingInstruction(string name, string text)
        {
            doThrowMethodNotUse();
        }

        // Writes out an entity reference as follows: "&"+name+";".
        public override void WriteEntityRef(string name)
        {
            doThrowMethodNotUse();
        }

        // Forces the generation of a character entity for the specified Unicode character value.
        public override void WriteCharEntity(char ch)
        {
            doThrowMethodNotUse();
        }

        // Writes out the given whitespace. 
        public override void WriteWhitespace(string ws)
        {
            doThrowMethodNotUse();
        }

        // Writes out the specified text content.
        public override void WriteString(string text)
        {
            try
            {
                if (null != text && text.Length != 0)
                {
                    AutoComplete(Token.Content);
                    xmlEncoder.Write(text);
                }
            }
            catch
            {
                currentState = State.Error;
                throw;
            }
        }

        // Writes out the specified surrogate pair as a character entity.
        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            doThrowMethodNotUse();
        }


        // Writes out the specified text content.
        public override void WriteChars(Char[] buffer, int index, int count)
        {
            doThrowMethodNotUse();
        }

        // Writes raw markup from the specified character buffer.
        public override void WriteRaw(Char[] buffer, int index, int count)
        {
            doThrowMethodNotUse();
        }

        // Writes raw markup from the specified character string.
        public override void WriteRaw(String data)
        {
            doThrowMethodNotUse();
        }

        // Encodes the specified binary bytes as base64 and writes out the resulting text.
        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            doThrowMethodNotUse();
        }


        // Encodes the specified binary bytes as binhex and writes out the resulting text.
        public override void WriteBinHex(byte[] buffer, int index, int count)
        {
            doThrowMethodNotUse();
        }

        // Returns the state of the XmlWriter.
        public override WriteState WriteState
        {
            get
            {
                switch (this.currentState)
                {
                    case State.Start:
                        return WriteState.Start;
                    case State.Prolog:
                    case State.PostDTD:
                        return WriteState.Prolog;
                    case State.Element:
                        return WriteState.Element;
                    case State.Attribute:
                    case State.AttrOnly:
                        return WriteState.Attribute;
                    case State.Content:
                    case State.Epilog:
                        return WriteState.Content;
                    case State.Error:
                        return WriteState.Error;
                    case State.Closed:
                        return WriteState.Closed;
                    default:
                        Debug.Assert(false);
                        return WriteState.Error;
                }
            }
        }

        // Closes the XmlWriter and the underlying stream/TextWriter.
        public override void Close()
        {
            try
            {
                AutoCompleteAll();
            }
            catch
            { // never fail
            }
            finally
            {
                this.currentState = State.Closed;
                textWriter.Close();
            }
        }

        // Flushes whatever is in the buffer to the underlying stream/TextWriter and flushes the underlying stream/TextWriter.
        public override void Flush()
        {
            textWriter.Flush();
        }

        // Writes out the specified name, ensuring it is a valid Name according to the XML specification 
        // (http://www.w3.org/TR/1998/REC-xml-19980210#NT-Name
        public override void WriteName(string name)
        {
            doThrowMethodNotUse();
        }

        // Writes out the specified namespace-qualified name by looking up the prefix that is in scope for the given namespace.
        public override void WriteQualifiedName(string localName, string ns)
        {
            doThrowMethodNotUse();
        }

        // Returns the closest prefix defined in the current namespace scope for the specified namespace URI.
        public override string LookupPrefix(string ns)
        {
            throw new Exception(MethodNotUse);
        }

        // Gets an XmlSpace representing the current xml:space scope. 
        public  XmlSpace XmlSpace
        {
            get
            {
                for (int i = top; i > 0; i--)
                {
                    XmlSpace xs = stack[i].xmlSpace;
                    if (xs != XmlSpace.None)
                        return xs;
                }
                return XmlSpace.None;
            }
        }

        // Gets the current xml:lang scope.
        public override string XmlLang
        {
            get
            {
                for (int i = top; i > 0; i--)
                {
                    String xlang = stack[i].xmlLang;
                    if (xlang != null)
                        return xlang;
                }
                return null;
            }
        }

        // Writes out the specified name, ensuring it is a valid NmToken
        // according to the XML specification (http://www.w3.org/TR/1998/REC-xml-19980210#NT-Name).
        public override void WriteNmToken(string name)
        {
            doThrowMethodNotUse();
        }

        void AutoComplete(Token token)
        {
            if (this.currentState == State.Closed)
            {
                throw new InvalidOperationException("The XmlReader is closed or in error state.");
            }
            else if (this.currentState == State.Error)
            {
                throw new InvalidOperationException("Token {0} in state {1} would result in an invalid XML document.");
            }

            State newState = this.stateTable[(int)token * 8 + (int)this.currentState];
            if (newState == State.Error)
            {
                throw new InvalidOperationException("Token {0} in state {1} would result in an invalid XML document.");
            }

            switch (token)
            {
                case Token.Doctype:
                    if (this.indented && this.currentState != State.Start)
                    {
                        Indent(false);
                    }
                    break;

                case Token.StartElement:
                case Token.Comment:
                case Token.PI:
                case Token.CData:
                    if (this.currentState == State.Attribute)
                    {
                        WriteEndAttributeQuote();
                        WriteEndStartTag(false);
                    }
                    else if (this.currentState == State.Element)
                    {
                        WriteEndStartTag(false);
                    }
                    if (token == Token.CData)
                    {
                        stack[top].mixed = true;
                    }
                    else if (this.indented && this.currentState != State.Start)
                    {
                        Indent(false);
                    }
                    break;

                case Token.EndElement:
                case Token.LongEndElement:
                    if (this.flush)
                    {
                        FlushEncoders();
                    }
                    if (this.currentState == State.Attribute)
                    {
                        WriteEndAttributeQuote();
                    }
                    if (this.currentState == State.Content)
                    {
                        token = Token.LongEndElement;
                    }
                    else
                    {
                        WriteEndStartTag(token == Token.EndElement);
                    }
                    if (stateTableDocument == this.stateTable && top == 1)
                    {
                        newState = State.Epilog;
                    }
                    break;

                case Token.StartAttribute:
                    if (this.flush)
                    {
                        FlushEncoders();
                    }
                    if (this.currentState == State.Attribute)
                    {
                        WriteEndAttributeQuote();
                        textWriter.Write(' ');
                    }
                    else if (this.currentState == State.Element)
                    {
                        textWriter.Write(' ');
                    }
                    break;

                case Token.EndAttribute:
                    if (this.flush)
                    {
                        FlushEncoders();
                    }
                    WriteEndAttributeQuote();
                    break;

                case Token.Whitespace:
                case Token.Content:
                case Token.RawData:
                case Token.Base64:

                    if (token != Token.Base64 && this.flush)
                    {
                        FlushEncoders();
                    }
                    if (this.currentState == State.Element && this.lastToken != Token.Content)
                    {
                        WriteEndStartTag(false);
                    }
                    if (newState == State.Content)
                    {
                        stack[top].mixed = true;
                    }
                    break;

                default:
                    throw new InvalidOperationException("Operation is not valid due to the current state of the object.");
            }
            this.currentState = newState;
            this.lastToken = token;
        }

        void AutoCompleteAll()
        {

            if (this.flush)
            {
                FlushEncoders();
            }
            while (top > 0)
            {
                WriteEndElement();
            }
        }

        void InternalWriteEndElement(bool longFormat)
        {
            try
            {
                if (top <= 0)
                {
                    throw new InvalidOperationException("There was no XML start tag open.");
                }
                // if we are in the element, we need to close it.
                AutoComplete(longFormat ? Token.LongEndElement : Token.EndElement);
                if (this.lastToken == Token.LongEndElement)
                {
                    if (this.indented)
                    {
                        Indent(true);
                    }
                    textWriter.Write('<');
                    textWriter.Write('/');
                    if (this.namespaces && stack[top].prefix != null)
                    {
                        textWriter.Write(stack[top].prefix);
                        textWriter.Write(':');
                    }
                    textWriter.Write(stack[top].name);
                    textWriter.Write('>');
                }

                // pop namespaces
                int prevNsTop = stack[top].prevNsTop;
                if (useNsHashtable && prevNsTop < nsTop)
                {
                    PopNamespaces(prevNsTop + 1, nsTop);
                }
                nsTop = prevNsTop;
                top--;
            }
            catch
            {
                currentState = State.Error;
                throw;
            }
        }

        public void WriteNamespaces()
        {
            for (int i = nsTop; i > stack[top].prevNsTop; i--)
            {
                if (!nsStack[i].declared)
                {
                    textWriter.Write(" xmlns");
                    textWriter.Write(':');
                    textWriter.Write(nsStack[i].prefix);
                    textWriter.Write('=');
                    textWriter.Write(this.quoteChar);
                    xmlEncoder.Write(nsStack[i].ns);
                    textWriter.Write(this.quoteChar);
                }
            }
            // Default
            if ((stack[top].defaultNs != stack[top - 1].defaultNs) &&
                (stack[top].defaultNsState == NamespaceState.DeclaredButNotWrittenOut))
            {
                textWriter.Write(" xmlns");
                textWriter.Write('=');
                textWriter.Write(this.quoteChar);
                xmlEncoder.Write(stack[top].defaultNs);
                textWriter.Write(this.quoteChar);
                stack[top].defaultNsState = NamespaceState.DeclaredAndWrittenOut;
            }
        }

        void WriteEndStartTag(bool empty)
        {
            xmlEncoder.StartAttribute(false);
            xmlEncoder.EndAttribute();
            if (empty)
            {
                textWriter.Write(" /");
            }
            textWriter.Write('>');
        }

        void WriteEndAttributeQuote()
        {
            if (this.specialAttr != SpecialAttr.None)
            {
                // Ok, now to handle xmlspace, etc.
                HandleSpecialAttribute();
            }
            xmlEncoder.EndAttribute();
            textWriter.Write(this.curQuoteChar);
        }

        void Indent(bool beforeEndElement)
        {
            // pretty printing.
            if (top == 0)
            {
                textWriter.WriteLine();
            }
            else if (!stack[top].mixed)
            {
                textWriter.WriteLine();
                int i = beforeEndElement ? top - 1 : top;
                for (i *= this.indentation; i > 0; i--)
                {
                    textWriter.Write(this.indentChar);
                }
            }
        }

        // pushes new namespace scope, and returns generated prefix, if one
        // was needed to resolve conflicts.
        void PushNamespace(string prefix, string ns, bool declared)
        {
            if (XmlReservedNs.NsXmlNs == ns)
            {
                throw new ArgumentException("Cannot bind to the reserved namespace.");
            }

            if (prefix == null)
            {
                switch (stack[top].defaultNsState)
                {
                    case NamespaceState.DeclaredButNotWrittenOut:
                        Debug.Assert(declared == true, "Unexpected situation!!");
                        // the first namespace that the user gave us is what we
                        // like to keep. 
                        break;
                    case NamespaceState.Uninitialized:
                    case NamespaceState.NotDeclaredButInScope:
                        // we now got a brand new namespace that we need to remember
                        stack[top].defaultNs = ns;
                        break;
                    default:
                        Debug.Assert(false, "Should have never come here");
                        return;
                }
                stack[top].defaultNsState = (declared ? NamespaceState.DeclaredAndWrittenOut : NamespaceState.DeclaredButNotWrittenOut);
            }
            else
            {
                if (prefix.Length != 0 && ns.Length == 0)
                {
                    throw new ArgumentException("Cannot use a prefix with an empty namespace.");
                }

                int existingNsIndex = LookupNamespace(prefix);
                if (existingNsIndex != -1 && nsStack[existingNsIndex].ns == ns)
                {
                    // it is already in scope.
                    if (declared)
                    {
                        nsStack[existingNsIndex].declared = true;
                    }
                }
                else
                {
                    // see if prefix conflicts for the current element
                    if (declared)
                    {
                        if (existingNsIndex != -1 && existingNsIndex > stack[top].prevNsTop)
                        {
                            nsStack[existingNsIndex].declared = true; // old one is silenced now
                        }
                    }
                    AddNamespace(prefix, ns, declared);
                }
            }
        }

        void AddNamespace(string prefix, string ns, bool declared)
        {
            int nsIndex = ++nsTop;
            if (nsIndex == nsStack.Length)
            {
                Namespace[] newStack = new Namespace[nsIndex * 2];
                Array.Copy(nsStack, newStack, nsIndex);
                nsStack = newStack;
            }
            nsStack[nsIndex].Set(prefix, ns, declared);

            if (useNsHashtable)
            {
                AddToNamespaceHashtable(nsIndex);
            }
            else if (nsIndex == MaxNamespacesWalkCount)
            {
                // add all
                nsHashtable = new Dictionary<string, int>(new SecureStringHasher());
                for (int i = 0; i <= nsIndex; i++)
                {
                    AddToNamespaceHashtable(i);
                }
                useNsHashtable = true;
            }
        }

        void AddToNamespaceHashtable(int namespaceIndex)
        {
            string prefix = nsStack[namespaceIndex].prefix;
            int existingNsIndex;
            if (nsHashtable.TryGetValue(prefix, out existingNsIndex))
            {
                nsStack[namespaceIndex].prevNsIndex = existingNsIndex;
            }
            nsHashtable[prefix] = namespaceIndex;
        }

        private void PopNamespaces(int indexFrom, int indexTo)
        {
            Debug.Assert(useNsHashtable);
            for (int i = indexTo; i >= indexFrom; i--)
            {
                Debug.Assert(nsHashtable.ContainsKey(nsStack[i].prefix));
                if (nsStack[i].prevNsIndex == -1)
                {
                    nsHashtable.Remove(nsStack[i].prefix);
                }
                else
                {
                    nsHashtable[nsStack[i].prefix] = nsStack[i].prevNsIndex;
                }
            }
        }

        string GeneratePrefix()
        {
            int temp = stack[top].prefixCount++ + 1;
            return "d" + top.ToString("d", CultureInfo.InvariantCulture)
                + "p" + temp.ToString("d", CultureInfo.InvariantCulture);
        }

        void InternalWriteProcessingInstruction(string name, string text)
        {
            textWriter.Write("<?");
            ValidateName(name, false);
            textWriter.Write(name);
            textWriter.Write(' ');
            if (null != text)
            {
                xmlEncoder.WriteRawWithSurrogateChecking(text);
            }
            textWriter.Write("?>");
        }

        int LookupNamespace(string prefix)
        {
            if (useNsHashtable)
            {
                int nsIndex;
                if (nsHashtable.TryGetValue(prefix, out nsIndex))
                {
                    return nsIndex;
                }
            }
            else
            {
                for (int i = nsTop; i >= 0; i--)
                {
                    if (nsStack[i].prefix == prefix)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        int LookupNamespaceInCurrentScope(string prefix)
        {
            if (useNsHashtable)
            {
                int nsIndex;
                if (nsHashtable.TryGetValue(prefix, out nsIndex))
                {
                    if (nsIndex > stack[top].prevNsTop)
                    {
                        return nsIndex;
                    }
                }
            }
            else
            {
                for (int i = nsTop; i > stack[top].prevNsTop; i--)
                {
                    if (nsStack[i].prefix == prefix)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        string FindPrefix(string ns)
        {
            for (int i = nsTop; i >= 0; i--)
            {
                if (nsStack[i].ns == ns)
                {
                    if (LookupNamespace(nsStack[i].prefix) == i)
                    {
                        return nsStack[i].prefix;
                    }
                }
            }
            return null;
        }

        // This method is used for validation of the DOCTYPE, processing instruction and entity names plus names 
        // written out by the user via WriteName and WriteQualifiedName.
        // Unfortunatelly the names of elements and attributes are not validated by the XmlTextWriter.
        // Also this method does not check wheather the character after ':' is a valid start name character. It accepts
        // all valid name characters at that position. This can't be changed because of backwards compatibility.
        private void ValidateName(string name, bool isNCName)
        {
            if (name == null || name.Length == 0)
            {
                throw new ArgumentException("The empty string '' is not a valid name.");
            }

            int nameLength = name.Length;

            // Namespaces supported
            if (namespaces)
            {
                // We can't use ValidateNames.ParseQName here because of backwards compatibility bug we need to preserve.
                // The bug is that the character after ':' is validated only as a NCName characters instead of NCStartName.
                int colonPosition = -1;

                // Parse NCName (may be prefix, may be local name)
                int position = ValidateNames.ParseNCName(name);

            Continue:
                if (position == nameLength)
                {
                    return;
                }

                // we have prefix:localName
                if (name[position] == ':')
                {
                    if (!isNCName)
                    {
                        // first colon in qname
                        if (colonPosition == -1)
                        {
                            // make sure it is not the first or last characters
                            if (position > 0 && position + 1 < nameLength)
                            {
                                colonPosition = position;
                                // Because of the back-compat bug (described above) parse the rest as Nmtoken
                                position++;
                                position += ValidateNames.ParseNmtoken(name, position);
                                goto Continue;
                            }
                        }
                    }
                }
            }
            // Namespaces not supported
            else
            {
                if (ValidateNames.IsNameNoNamespaces(name))
                {
                    return;
                }
            }
            throw new ArgumentException("Invalid name character in '{0}'.", name);
        }

        void HandleSpecialAttribute()
        {
            string value = xmlEncoder.AttributeValue;
            switch (this.specialAttr)
            {
                case SpecialAttr.XmlLang:
                    stack[top].xmlLang = value;
                    break;
                case SpecialAttr.XmlSpace:
                    // validate XmlSpace attribute
                    value = TrimString(value);
                    if (value == "default")
                    {
                        stack[top].xmlSpace = XmlSpace.Default;
                    }
                    else if (value == "preserve")
                    {
                        stack[top].xmlSpace = XmlSpace.Preserve;
                    }
                    else
                    {
                        throw new ArgumentException("'{0}' is an invalid xml:space value.", value);
                    }
                    break;
                case SpecialAttr.XmlNs:
                    VerifyPrefixXml(this.prefixForXmlNs, value);
                    PushNamespace(this.prefixForXmlNs, value, true);
                    break;
            }
        }

        void VerifyPrefixXml(string prefix, string ns)
        {

            if (prefix != null && prefix.Length == 3)
            {
                if (
                   (prefix[0] == 'x' || prefix[0] == 'X') &&
                   (prefix[1] == 'm' || prefix[1] == 'M') &&
                   (prefix[2] == 'l' || prefix[2] == 'L')
                   )
                {
                    if (XmlReservedNs.NsXml != ns)
                    {
                        throw new ArgumentException("Prefixes beginning with \"xml\" (regardless of whether the characters are uppercase, lowercase, or some combination thereof) are reserved for use by XML.");
                    }
                }
            }
        }

        void PushStack()
        {
            if (top == stack.Length - 1)
            {
                TagInfo[] na = new TagInfo[stack.Length + 10];
                if (top > 0) Array.Copy(stack, na, top + 1);
                stack = na;
            }

            top++; // Move up stack
            stack[top].Init(nsTop);
        }

        void FlushEncoders()
        {
            this.flush = false;
        }

        internal static readonly char[] WhitespaceChars = new char[] { ' ', '\t', '\n', '\r' };
        internal static string TrimString(string value)
        {
            return value.Trim(WhitespaceChars);
        }
    }

    internal class SecureStringHasher : IEqualityComparer<String>
    {
        [SecurityCritical]
        delegate int HashCodeOfStringDelegate(string s, int sLen, long additionalEntropy);

        // Value is guaranteed to be null by the spec.
        // No explicit assignment because it will require adding SecurityCritical on .cctor
        // which could hurt the performance
        [SecurityCritical]
        static HashCodeOfStringDelegate hashCodeDelegate;

        int hashCodeRandomizer;

        public SecureStringHasher()
        {
            this.hashCodeRandomizer = Environment.TickCount;
        }

        public bool Equals(String x, String y)
        {
            return String.Equals(x, y, StringComparison.Ordinal);
        }

        [SecuritySafeCritical]
        public int GetHashCode(String key)
        {
            if (hashCodeDelegate == null)
            {
                hashCodeDelegate = GetHashCodeDelegate();
            }
            return hashCodeDelegate(key, key.Length, hashCodeRandomizer);
        }

        [SecurityCritical]
        private static int GetHashCodeOfString(string key, int sLen, long additionalEntropy)
        {
            int hashCode = unchecked((int)additionalEntropy);
            // use key.Length to eliminate the rangecheck
            for (int i = 0; i < key.Length; i++)
            {
                hashCode += (hashCode << 7) ^ key[i];
            }
            // mix it a bit more
            hashCode -= hashCode >> 17;
            hashCode -= hashCode >> 11;
            hashCode -= hashCode >> 5;
            return hashCode;
        }

        [SecuritySafeCritical]
        private static HashCodeOfStringDelegate GetHashCodeDelegate()
        {
            // If we find the Marvin hash method, we use that
            // Otherwise, we use the old string hashing function.

            MethodInfo getHashCodeMethodInfo = typeof(String).GetMethod("InternalMarvin32HashString", BindingFlags.NonPublic | BindingFlags.Static);
            if (getHashCodeMethodInfo != null)
            {
                return (HashCodeOfStringDelegate)Delegate.CreateDelegate(typeof(HashCodeOfStringDelegate), getHashCodeMethodInfo);
            }

            // This will fall through and return a delegate to the old hash function
            Debug.Assert(false, "Randomized hashing is not supported.");

            return new HashCodeOfStringDelegate(GetHashCodeOfString);
        }
    }
}

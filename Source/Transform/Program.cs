using System;
using System.IO;
using System.Text;

namespace GostCryptography.Xml.SmevTransformSpi
{
    internal class Program
    {
        static string testStr1 = 
 @"<qwe xmlns=""http://t.e.s.t"">
 <iop1 value="" test ""/>
 <myns:rty xmlns:myns=""http://y.e.s"">yes!</myns:rty>
 <iop value="" test ""/>     </qwe>";


        static string testStr2 =
 //@"<zerno:MessageData xmlns:zerno=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/ws/types/1.0.0"" Id=""SIGNED_BY_CALLER""><zerno:MessageID>3eb6773e-fed4-11ec-b786-e2b1cb7e6353</zerno:MessageID><zerno:ReferenceMessageID>3eb6773e-fed4-11ec-b786-e2b1cb7e6353</zerno:ReferenceMessageID><zerno:MessagePrimaryContent><dicts:Request xmlns:dicts=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/ws/dictionaries/1.0.0""><dicts:Dictionary>ОКПД2</dicts:Dictionary></dicts:Request></zerno:MessagePrimaryContent></zerno:MessageData>";
 //@"<qwe xmlns=""http://t.e.s.t"">
 // <myns:rty xmlns:myns=""http://y.e.s"" myns:nouse=""http://nouse"">yes!</myns:rty>
 // <iop value=""yes, yes!"" />
 //</qwe>";
 //@"<tns:PERNAMEZPRequest xmlns:markcont=""urn://x-artefacts-zags-pernamezp/markertypes/4.0.0"" xmlns:tns=""urn://x-artefacts-zags-pernamezp/4.0.0"" xmlns:fnst=""urn://x-artefacts-zags-pernamezp/types/4.0.0""  xmlns:frgu=""urn://x-artefacts-zags-pernamezp/frgutypes/4.0.0"" markcont:ТипЗаявл=""ФЛ"" ЗаявлДата=""2019-08-13"" tns:ИдСвед=""a"" markcont:Заявление=""15843"" ДатаСвед=""2018-08-13"" frgu:КодУслуги=""3482943""/>";
 @"<ns:RequestCreateExtinction SDIZNumber=""string"" amount=""100"" fullExtinction=""false"" xmlns:ns=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/ws/sdiz/1.0.0"" xmlns:ns1=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/sdiz/1.0.0"" xmlns:ns2=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/common/1.0.0"">
  <!--Zero or more repetitions:-->
  <ns1:TransportInfo numberTransport=""string"" numberСontainer=""string"">
    <ns2:TransportCode>string</ns2:TransportCode>
  </ns1:TransportInfo>
  <!--Optional:-->
  <ns1:WeightDiscperancyCause>string</ns1:WeightDiscperancyCause>
  <!--Optional:-->
  <ns1:CauseComment>string</ns1:CauseComment>
</ns:RequestCreateExtinction>";

        static void Main(string[] args)
        {
            MemoryStream src = new MemoryStream(Encoding.UTF8.GetBytes(testStr2));
            src.Position = 0;
            MemoryStream dst = new MemoryStream();

            SmevTransformSpi tramsform = new SmevTransformSpi();
            tramsform.process(src, dst);
            string result = Encoding.UTF8.GetString(dst.ToArray());
        }
    }
}

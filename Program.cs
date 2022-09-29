using System;
using System.IO;
using System.Text;

namespace SmevTransformSpi
{
    internal class Program
    {
        static string testStr1 = 
 @"<qwe xmlns=""http://t.e.s.t"">
 <iop1 value="" test ""/>
 <myns:rty xmlns:myns=""http://y.e.s"">yes!</myns:rty>
 <iop value="" test ""/>     </qwe>";


        static string testStr2 =
 @"<zerno:MessageData xmlns:zerno=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/ws/types/1.0.0"" Id=""SIGNED_BY_CALLER""><zerno:MessageID>3eb6773e-fed4-11ec-b786-e2b1cb7e6353</zerno:MessageID><zerno:ReferenceMessageID>3eb6773e-fed4-11ec-b786-e2b1cb7e6353</zerno:ReferenceMessageID><zerno:MessagePrimaryContent><dicts:Request xmlns:dicts=""urn://x-artefacts-mcx-gov-ru/fgiz-zerno/api/ws/dictionaries/1.0.0""><dicts:Dictionary>ОКПД2</dicts:Dictionary></dicts:Request></zerno:MessagePrimaryContent></zerno:MessageData>";

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

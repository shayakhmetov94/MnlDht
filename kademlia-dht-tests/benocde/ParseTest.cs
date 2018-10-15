using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using mainline_dht.Base.Bittorrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace mainline_dht_tests.benocde
{
    [TestClass]
    public class ParseTest
    {
        public static byte[] ConvertHexStringToByteArray(string hexString) {
            return SoapHexBinary.Parse(hexString).Value;
        }

        [TestMethod]
        public void ParseFindNode() {
            string findNodeMsg = "64313a6164323a696432303a5fa2a8ea6689e6beaa61b93196b10f4404dac592363a74617267657432303a5fa2a8ea6689e6beaa61b93196b10f4404dac59265313a71393a66696e645f6e6f6465313a74323aaf9e313a76343a4c540100313a79313a7165";

            Bencoder bc = new Bencoder();

            byte[] msgAsBytes = ConvertHexStringToByteArray(findNodeMsg);
            string asciiMsg = Encoding.ASCII.GetString(msgAsBytes);

            Dictionary<string, object> fields = (Dictionary<string, object>)bc.DecodeElement(msgAsBytes);

        }

        [TestMethod]
        public void ParsePing() {
            string pingMsg = "64313a6164323a696432303a5fa2a2809655927cbbe6fdab912c88e59a4630b065313a71343a70696e67313a74323a0136313a76343a4c540101313a79313a7165";
            string unicodePingMsg = Encoding.ASCII.GetString(ConvertHexStringToByteArray(pingMsg));
            string myPingMsg = "64313a74323a3032313a79313a71313a71343a70696e67313a6164323a696432303aefbfbd55efbfbd4a43efbfbd23efbfbdefbfbd20efbfbdefbfbdefbfbdefbfbd113738efbfbdefbfbd176565";
            string unicodeMyPingMsg = Encoding.ASCII.GetString(ConvertHexStringToByteArray(myPingMsg));
            Bencoder bc = new Bencoder();

            Dictionary<string, object> fields = (Dictionary<string, object>)bc.DecodeElement(ConvertHexStringToByteArray(pingMsg));
            Dictionary<string, object> myfields = (Dictionary<string, object>)bc.DecodeElement(ConvertHexStringToByteArray(myPingMsg));
            int boop = 1;
        }

        [TestMethod]
        public void ParseProblematicMsg() {
            string problematicMsg = "d1:rd2:id20:????g????r=)??HWu5:nodes208:?Jk??\" ??? jm ?? (???[?3????[E?{??ZU?x??,???????ln?|^L@??cxQkXT/???c??=Z??2?U??H??m?*Y?D??	K????????V???('.??=	??B96?????h?S? V????t??????q? j???O?|^?b? p???????????I??";
            var decoded = new Bencoder().DecodeElement(Encoding.ASCII.GetBytes(problematicMsg));

        }

        [TestMethod]
        public void ParseMsgWithProblematicList() {
            byte[] problematicMsg = ParseTest.ConvertHexStringToByteArray( "64313A7264323A696432303A30643513245B078D06870AA59DB1F7A21EC41608353A746F6B656E323A6778363A76616C7565736C6565313A74323A4635313A79313A7265");
            var decoded = new Bencoder().DecodeElement(problematicMsg);

        }

    }


}

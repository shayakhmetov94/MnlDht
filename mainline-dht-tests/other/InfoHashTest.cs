using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using mainline_dht.Base.Bittorrent;
using mainline_dht.Base.Message;
using mainline_dht_tests.benocde;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamingTest.Bittorrent;

namespace kademlia_dht_tests.other
{
    [TestClass]
    public class InfoHashTest
    {
        [TestMethod]
        public void CalcInfoHash() {
            Metainfo mf = new Metainfo(File.ReadAllBytes("infHash.torrent"), Encoding.ASCII);

            CollectionAssert.AreEqual(mf.InfoHash, ParseTest.ConvertHexStringToByteArray("139f69de76a07790ec3ad31cbc3bd5d905871600"));
        }

        [TestMethod]
        public void ExtractInfoHash() {
            Bencoder bc = new Bencoder();
            MnlMessage msg = new MnlMessage
                            .Builder(ParseTest.ConvertHexStringToByteArray("64313a6164323a696432303a7200ee612a3d17f22d721a8019317eb27c7039c9393a696e666f5f6861736832303af6ef92f7cfa792c050fef70c405ded104771553d363a6e6f7365656469316565313a71393a6765745f7065657273313a74343a9b9427a4313a76343a5554ab14313a79313a7165"))
                            .Build();

            MnlMessage myMsg = new MnlMessage
                               .Builder(ParseTest.ConvertHexStringToByteArray("64313a74323a3032313a79313a71313a71393a6765745f7065657273313a6164323a696432303a09dc0cea8478d7f285ef674c6a2f931ae3b603f4393a696e666f5f6861736832303a3f4f40163040223f3f063f3a562f603e3f543f3f6565"))
                               .Build();



            Dictionary<string, object> decoded = (Dictionary<string, object>)bc.DecodeElement(ParseTest.ConvertHexStringToByteArray("64313a6164323a696432303ab2dfb21aa9b31c00cdca99bcd9232a7facbc76e2393a696e666f5f6861736832303aa44f7b0766a4e53ebfc0cf79fa9c82778b88d94765313a71393a6765745f7065657273313a74343a67707588313a79313a7165"));
            Dictionary<string, object> decodedMy = (Dictionary<string, object>)bc.DecodeElement(ParseTest.ConvertHexStringToByteArray("64313a74323a3034313a79313a71313a71393a6765745f7065657273313a6164323a696432303ae36f5d8ef6928691d9696e02d025e3b8f1a08ca2393a696e666f5f6861736832303a139f69de76a07790ec3ad31cbc3bd5d9058716006565"));
            var infoHash = Encoding.UTF8.GetBytes((string)((Dictionary<string, object>)decoded["a"])["info_hash"]);

            var bytes = ParseTest.ConvertHexStringToByteArray("3F4F40163040223F3F063F3A562F603E3F543F3F");

            //CollectionAssert.AreEqual(infoHash, bytes);

            SoapHexBinary hex = new SoapHexBinary(infoHash);
            Debug.WriteLine($"Info hash: {hex.ToString()} ");
        }
    }
}

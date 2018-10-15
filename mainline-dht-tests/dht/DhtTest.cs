using System;
using System.Linq;
using System.Net;
using System.Text;
using mainline_dht.Base;
using mainline_dht.Base.Message;
using mainline_dht_tests.benocde;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace mainline_dht_tests.dht
{
    [TestClass]
    public class DhtTest
    {
        [TestMethod]
        public void FindPeers() {
            Node node = new Node(new System.Net.IPEndPoint(IPAddress.Any, 8881));

            ContactNode qbitContact = new ContactNode(Id.GenerateRandom(), new IPEndPoint(IPAddress.Loopback, 8999));

            MnlMessage pingResp = node.Ping(qbitContact);

            qbitContact = new ContactNode(new Id((byte[])pingResp.Payload["id"]), qbitContact.EndPoint);

            node.BucketList.Put(qbitContact);
            
            HashTable hashTable = new HashTable(node, new IPEndPoint[] { new IPEndPoint(IPAddress.Loopback, 8999) }, 8881);

            Id infoHash = new Id(ParseTest.ConvertHexStringToByteArray("139f69de76a07790ec3ad31cbc3bd5d905871600"));

            var foundPeers = hashTable.FindPeers(infoHash);

            Assert.IsTrue(foundPeers.Count() > 0);

            node.Shutdown();
        }
    }
}

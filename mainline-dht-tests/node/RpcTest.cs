using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using mainline_dht.Base;
using mainline_dht_tests.benocde;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace mainline_dht_tests
{
    [TestClass]
    public class RpcTest
    {
        [TestMethod]
        public void Ping() {
            Node node = new Node(new System.Net.IPEndPoint(IPAddress.Loopback, 8881));

            var recieved = node.Ping(new ContactNode(Id.GenerateRandom(), new IPEndPoint(IPAddress.Loopback, 8999)));
            Assert.IsNotNull(recieved);
            node.Shutdown();
        }

        [TestMethod]
        public void FindNodes() {
            Node node = new Node(new System.Net.IPEndPoint(IPAddress.Loopback, 8881));

            var recieved = node.FindNode(new ContactNode(Id.GenerateRandom(), new IPEndPoint(IPAddress.Loopback, 8999)), node.Id);
            Assert.IsTrue(recieved.Count() == 8);
            node.Shutdown();
        }

        [TestMethod]
        public void GetPeers() { // - new info hash 3F4F7B07663F3F3E3F3F3F793F3F3F773F3F3F47
            Node node = new Node(new System.Net.IPEndPoint(IPAddress.Loopback, 8881));
            var bytes = ParseTest.ConvertHexStringToByteArray("f6ef92f7cfa792c050fef70c405ded104771553d");
            //Array.Reverse(bytes);
            Id infoHash = new Id(bytes);

            CollectionAssert.AreEqual(infoHash.Value, ParseTest.ConvertHexStringToByteArray("f6ef92f7cfa792c050fef70c405ded104771553d"));

            IEnumerable<ContactNode> nodes = null;
            object token = null;

            Assert.IsTrue(node.GetPeers(new ContactNode(Id.GenerateRandom(), new IPEndPoint(IPAddress.Loopback, 8999)), infoHash, out nodes, out token));

            node.Shutdown();
        }
    }
}

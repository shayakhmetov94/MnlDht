using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace kademlia_dht_tests.other
{
    [TestClass]
    public class BufferOverflow
    {
        [TestMethod]
        public void MessageIdOverflow() {
            var arr = Encoding.ASCII.GetBytes("\0".PadLeft(2, '\0'));

            BitConverter.ToUInt16(arr, 0);
        }

        [TestMethod]
        public void LengthMismatch() {
            byte[] msg = new byte[] {
                0x00,
                0x01,
                0x02,
                0x03,
                0x04,
                0x05,
                0x06,
                0x07,
                0x08,
                0x09,
                0x10,
                0x11,
                0x12,
                0x13,
                0x14,
                0x00,
                0x16,
                0x17,
                0x18,
                0x19
            };

            string asciiString = Encoding.ASCII.GetString(msg);
            Assert.IsTrue(asciiString.Length == msg.Length, "Length mismatch");
            string utf8String = Encoding.UTF8.GetString(msg);
            Assert.IsTrue(utf8String.Length == msg.Length, "Length mismatch");
            //ID length mismathc
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base
{
    public class ContactNode
    {
        public Id Id { get; }
        public IPEndPoint EndPoint { get; private set; }
        public int UtpPort { get; set; }

        public ContactNode(Id id, IPEndPoint endPoint) {
            Id = id;
            EndPoint = endPoint;
        }

        //TODO: support ipv6
        public ContactNode(byte[] compactData, ref int offset) {
            byte[] idBytes = new byte[20]; //TODO: move to globals
            Buffer.BlockCopy(compactData, offset, idBytes, 0, idBytes.Length);
            byte[] ipv4Bytes = new byte[4];
            offset += idBytes.Length;
            Buffer.BlockCopy(compactData, offset, ipv4Bytes, 0, ipv4Bytes.Length);
            int port = 0; 
            offset += ipv4Bytes.Length;
            port |= compactData[offset];
            port = port << 8;
            port |= compactData[offset + 1];
            
            offset += 2;

            Id = new Id(idBytes);
            EndPoint = new IPEndPoint(new IPAddress(ipv4Bytes), port);
        }

        public static ContactNode FromPeerInfo(byte[] peerInfo) {
            int offset = 0;
            return FromPeerInfo(peerInfo, ref offset);
        }

        public static ContactNode FromPeerInfo(byte[] peerInfo, ref int offset ) {
            var ipv4 = new byte[4];
            Buffer.BlockCopy(peerInfo, offset, ipv4, 0, ipv4.Length);
            offset += ipv4.Length;
            int port = 0;
            port |= peerInfo[offset];
            port <<= 8;
            offset++;
            port |= peerInfo[offset];

            return new ContactNode(null, new IPEndPoint(new IPAddress(ipv4), port)) { UtpPort = port };
        }

        public byte[] ToBytes(bool asUtpPeer = false) {
            MemoryStream bytesStream = new MemoryStream(26);
            if(!asUtpPeer) {
                bytesStream.Write(Id.Value, 0, Id.Value.Length);
            }

            byte[] ipBytes = EndPoint.Address.GetAddressBytes();
            bytesStream.Write(ipBytes, 0, ipBytes.Length);
            byte[] portBytes = BitConverter.GetBytes(asUtpPeer ? UtpPort : EndPoint.Port);
            
            bytesStream.WriteByte(portBytes[1]);
            bytesStream.WriteByte(portBytes[2]);

            return bytesStream.ToArray();
        }

        public override string ToString() {
            return $"Id: {Id.GetNumericValue()} | Address: {EndPoint.Address}:{EndPoint.Port}";
        }

        public class IdComparer : IComparer<ContactNode>
        {
            public int Compare( ContactNode x, ContactNode y ) {
                return x.Id.GetNumericValue().CompareTo( y.Id.GetNumericValue() );
            }
        }

        public class IpEqComparer : IEqualityComparer<ContactNode>
        {
            public bool Equals(ContactNode x, ContactNode y) {
                return x.EndPoint.Address.GetHashCode().Equals(y.EndPoint.Address.GetHashCode());
            }

            public int GetHashCode(ContactNode obj) {
                return obj.EndPoint.Address.GetHashCode();
            }
        }

        public class IpComparer : IComparer<ContactNode>
        {
            public int Compare(ContactNode x, ContactNode y) {
                return x.EndPoint.Address.GetHashCode().CompareTo(y.EndPoint.Address.GetHashCode());
            }
        }
    }


}

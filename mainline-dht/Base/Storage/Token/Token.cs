using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Storage.Token
{
    public class Token
    {
        private static RNGCryptoServiceProvider _csp = new RNGCryptoServiceProvider();

        public int Secret { get; private set; }
        public IPAddress Address { get; private set; }
        public DateTime Timestamp { get; private set; }

        private Token() { }

        /// <summary>
        /// Creates random token for ip address
        /// </summary>
        public Token(IPAddress address){
            Address = address;
            byte[] intBytes = new byte[sizeof(int)];
            _csp.GetBytes(intBytes);
            Secret = BitConverter.ToInt32(intBytes, 0);
            Timestamp = DateTime.UtcNow;
        }

        public byte[] AsBytes(){
            MemoryStream ms = new MemoryStream(20);
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(Secret); //4
            bw.Write(Address.ToString()); //7
            bw.Write(Timestamp.ToBinary()); //8
            bw.Write((byte)1);
            bw.Flush();
            ms.Flush();

            return ms.ToArray();
        }

        public static Token FromBytes(byte[] buff) {
            BinaryReader br = new BinaryReader(new MemoryStream(buff));
            int secret = br.ReadInt32();
            IPAddress address = IPAddress.Parse(br.ReadString());
            DateTime timestamp = DateTime.FromBinary(br.ReadInt64());
            Token newToken = new Token();
            newToken.Secret = secret;
            newToken.Address = address;
            newToken.Timestamp = timestamp;

            return newToken;
        }
    }
}

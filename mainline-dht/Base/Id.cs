using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base
{
    public class Id
    {
        private BigInteger _numericVal = BigInteger.Zero;

        public byte[] Value { get; }

        public Id(byte[] value) {
            if(value == null || value.Length < 20 || value.Length > 20)
                throw new ArgumentException("value");

            byte[] valueCopy = new byte[value.Length];
            Buffer.BlockCopy(value, 0, valueCopy, 0, value.Length);
            Value = valueCopy;
        }

        public BigInteger GetNumericValue() {
            if ( _numericVal.IsZero ) {
                byte[] valWithZeroByte = new byte[Value.Length + 1];
                if ( BitConverter.IsLittleEndian )
                    Buffer.BlockCopy( Value, 0, valWithZeroByte, 0, Value.Length );
                else
                    Buffer.BlockCopy( Value, 0, valWithZeroByte, 1, Value.Length );

                _numericVal = new BigInteger( valWithZeroByte );
            }

            return _numericVal;
        } 

        public static Id operator ^( Id first, Id second ) {
            byte[] result = new byte[20];
            Buffer.BlockCopy( first.Value, 0, result, 0, 20 );
            for ( int i = 0; i < 20; i++ )
                result[i] ^= second.Value[i];

            return new Id( result );
        }

        public static bool operator <( Id first, Id second ) {

            return first.GetNumericValue() < second.GetNumericValue();
        }

        public static bool operator >( Id first, Id second ) {

            return first.GetNumericValue() < second.GetNumericValue();
        }

        public static bool operator >=( Id first, Id second ) {

            return first.GetNumericValue() <= second.GetNumericValue();
        }

        public static bool operator <=( Id first, Id second ) {

            return first.GetNumericValue() <= second.GetNumericValue();
        }

        public static bool operator ==(Id first, Id second) {
            if((object)first == null || (object)second == null) {
                return false;
            }

            return first.GetNumericValue() == second.GetNumericValue();
        }

        public static bool operator !=(Id first, Id second) {
            if((object)first == null || (object)second == null) {
                return true;
            }

            return first.GetNumericValue() != second.GetNumericValue();
        }


        /// <summary>
        /// Generate random Id. Uses System.Security.Cryptography.RNGCryptoServiceProvider 
        /// </summary>
        /// <returns>New random Id</returns>
        public static Id GenerateRandom() {
            char[] printableChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] data = new byte[20];
            using(RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider()) {
                data = new byte[20];
                crypto.GetNonZeroBytes(data);
            } 

            StringBuilder result = new StringBuilder(20);
            foreach(byte b in data) {
                result.Append(printableChars[b % (printableChars.Length)]);
            }

            return new Id(Encoding.UTF8.GetBytes(result.ToString()));
        }

        public override bool Equals(object obj) {
            if(obj is Id id) {
                this.GetNumericValue().Equals(id.GetNumericValue());
            }
            
            return base.Equals(obj);
        }

        public override int GetHashCode() {
            return this.GetNumericValue().GetHashCode();
        }

        #region Comparers and Comparators
        public class EqualityComparer : IEqualityComparer<Id>
        {
            public bool Equals( Id x, Id y ) {
                return x.GetNumericValue() == y.GetNumericValue();
            }

            public int GetHashCode( Id obj ) {
                return obj.GetNumericValue().GetHashCode();
            }
        }

        public class KadIdToBaseComparator : IComparer<ContactNode>
        {
            private Id _base;

            public KadIdToBaseComparator( Id baseId ) {
                _base = baseId;
            }

            public int Compare( ContactNode x, ContactNode y ) {
                return _base.GetNumericValue().CompareTo( y.Id.GetNumericValue() );
            }
        }

        public class IdComparer : IComparer<Id>
        {
            public int Compare( Id x, Id y ) {
                return x.GetNumericValue().CompareTo( y.GetNumericValue() );
            }
        }
        #endregion
    }
}

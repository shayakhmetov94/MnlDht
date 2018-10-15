using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Bittorrent
{
    /// <summary>
    /// Бенкодер. Потокобезопасный. 
    /// </summary>
    public class Bencoder
    {
        static readonly object sync = new object();

        private int idx;
        private byte[] bencode;

        private StringBuilder sb;

        private Bencoder() {
            idx = 0;
            bencode = null;
            sb = new StringBuilder();
        }

        public static Bencoder Build() {
            lock ( sync ) {
                return new Bencoder();
            }
        }

        private List<dynamic> parseList() {
            List<dynamic> list = new List<dynamic>();
            do {
                list.Add( DecodeElement() );
            } while ( bencode[idx] != 'e' );
            idx++; //skip 'e'
            return list;
        }

        private Dictionary<string, dynamic> parseDictionary() {
            Dictionary<string, dynamic> dict = new Dictionary<string, dynamic>();
            string key;
            do {
                key = DecodeElement();
                dict.Add( key, DecodeElement() );
            } while ( bencode[idx] != 'e' );
            idx++; //skip 'e'
            return dict;
        }

        /// <summary>
        /// Декодирует данные из потока в bencode
        /// </summary>
        /// <param name="bencode">Строка bencode, которую нужно декодировать</param>
        /// <returns>Возвращает полученный объект (строка, массив, словарь)</returns>
        public dynamic Decode( byte[] bencode ) {
            this.bencode = bencode;
            idx = 0;
            return DecodeElement();
        }

        /// <summary>
        /// Кодирует строки, массивы, словари в bencode. Выбрасывает ArgumentException, если тип не поддерживается 
        /// </summary>
        /// <param name="obj">Объект, которого нужно кодировать</param>
        /// <returns>Bencode строка <code>obj</code></returns>
        public byte[] Encode( dynamic obj ) {
            return EncodeElement( obj );
        }

        private byte[] EncodeElement( dynamic obj ) {
            List<byte> bytesBuilder = new List<byte>();
            if ( obj is IDictionary<string, dynamic> ) {
                dynamic val;
                bytesBuilder.Add( (byte)'d' );
                foreach ( string key in obj.Keys ) {
                    bytesBuilder.AddRange( stringToBytes( Convert.ToString( key.Length ) + ':' + key ) );
                    obj.TryGetValue( key, out val );
                    bytesBuilder.AddRange( EncodeElement( val ) );
                }
                bytesBuilder.Add( (byte)'e' );

                return bytesBuilder.ToArray();
            }
            if ( obj is IList<dynamic> ) {
                bytesBuilder.Add( (byte)'l' );
                foreach ( var val in obj ) {
                    bytesBuilder.AddRange( EncodeElement( val ) );
                }
                bytesBuilder.Add( (byte)'e' );
                return bytesBuilder.ToArray();
            }
            if ( obj is int || obj is long ) {
                bytesBuilder.Add( (byte)'i' );
                bytesBuilder.AddRange( stringToBytes( Convert.ToString( obj ) ) );
                bytesBuilder.Add( (byte)'e' );
                return bytesBuilder.ToArray();
            }
            if ( obj is string ) {
                bytesBuilder.AddRange( stringToBytes( Convert.ToString( obj.Length ) ) );
                bytesBuilder.Add( (byte)':' );
                bytesBuilder.AddRange( stringToBytes( obj ) );
                return bytesBuilder.ToArray();
            }

            throw new ArgumentException( "Type " + obj.GetType() + " does not supported." );
        }

        private byte[] stringToBytes( string str ) {
            int len = str.Length;
            byte[] buf = new byte[len];
            char[] chars = str.ToCharArray();
            for ( int i = 0; i < len; i++ ) {
                buf[i] = (byte)chars[i];
            }
            return buf;
        }

        private dynamic DecodeElement() {
            int tval = bencode[idx++];
            dynamic retrVal;
            switch ( tval ) {
                case 'i':
                    do {
                        sb.Append( (char)bencode[idx++] );
                    } while ( bencode[idx] != 'e' );
                    idx++; //skip 'e'
                    retrVal = long.Parse( sb.ToString() );
                    sb.Clear();
                    break;
                case 'l':
                    retrVal = parseList();
                    break;
                case 'd':
                    retrVal = parseDictionary();
                    return retrVal;
                case 'e': //got something empty
                    idx--; //push 'e'
                    return null;
                default: //implying string
                    sb.Append( (char)tval );
                    while ( (tval = bencode[idx++]) != ':' ) { sb.Append( (char)tval ); }
                    int length = Int32.Parse(sb.ToString());
                    sb.Clear();
                    for ( int i = 0; i < length; i++ )
                        sb.Append( (char)bencode[i + idx] );
                    idx += length;
                    retrVal = sb.ToString();
                    sb.Clear();
                    break;
            }

            return retrVal;
        }
    }
}

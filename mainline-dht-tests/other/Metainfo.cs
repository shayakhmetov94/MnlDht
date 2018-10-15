using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using Bittorrent;
using System.Text;

namespace StreamingTest.Bittorrent
{
    public class Metainfo
    {
        private const string PieceLengthKey = "piece length";
        private const string InfoDictionaryKey = "info";
        private const string PiecesKey = "pieces";
        private const string FilesKey = "files";
        private const string LengthKey = "length";
        private const string MD5Key = "md5sum";
        private const string PathKey = "path";
        private const string AnnounceListKey = "announce-list";

        private readonly Bencoder _bencoder;
        private readonly Encoding _defaultEncoding;

        public Metainfo( byte[] data, Encoding encoding ) {
            _defaultEncoding = encoding;
            _bencoder = Bencoder.Build();
            var metainfoDict = _bencoder.Decode(data);
            var infoDict = metainfoDict[InfoDictionaryKey];

            PieceLength = infoDict[PieceLengthKey];
            // ReSharper disable once PossibleLossOfFraction
            PiecesHash = infoDict[PiecesKey];
            InfoHash = CalcInfoHashBytes( infoDict );
            Announces = GetAnnounces( metainfoDict );
            Files = GetFiles( infoDict );
            TotalLength = CalcTotalLength( Files );
            PiecesCount = (int)Math.Ceiling( (double)(TotalLength / PieceLength) ) + 1;
        }

        public long PieceLength { get; }
        public byte[] InfoHash { get; }
        public long TotalLength { get; }
        public List<File> Files { get; }
        public string PiecesHash { get; }
        public long PiecesCount { get; }
        public List<string> Announces { get; }

        private byte[] CalcInfoHashBytes( dynamic infoDict ) {
            byte[] encodedInfo = _bencoder.Encode(infoDict);
            var sha1Algo = SHA1.Create();
            return sha1Algo.ComputeHash( encodedInfo );
        }

        private long CalcTotalLength(List<File> files) {
            long totalLength = 0;
            foreach(var file in files) 
                totalLength += file.Length;

            return totalLength;
        }


        private List<File> GetFiles( dynamic infoDict ) {
            List<File> files = new List<File>(1);
            if ( infoDict.ContainsKey( FilesKey ) ) {
                var filesInfo = (List<dynamic>) infoDict[FilesKey];
                foreach ( var fileInfo in filesInfo ) {
                    File file = new File();
                    file.Length = fileInfo[LengthKey];

                    if ( infoDict.ContainsKey( MD5Key ) )
                        file.Md5Sum = fileInfo[MD5Key];

                    file.Path = _bencoder.Decode( _defaultEncoding.GetBytes(fileInfo[PathKey]));
                    files.Add( file );
                }
            } else {
                File file = new File();
                file.Length = infoDict[LengthKey];

                if ( infoDict.ContainsKey( MD5Key ) )
                    file.Md5Sum = infoDict[MD5Key];

                if ( infoDict.ContainsKey( PathKey ) )
                    file.Path = _bencoder.Decode( _defaultEncoding.GetBytes(infoDict[PathKey]) );

                files.Add( file );
            }

            return files;
        }



        private List<string> GetAnnounces( dynamic meta ) {
            List < string > announces = new List<string>( );
            if ( !meta.ContainsKey( AnnounceListKey ) )
                return announces;
            
            dynamic announceList = meta[AnnounceListKey];
            foreach ( dynamic announce in announceList ) {
                announces.Add( announce[0] );
            }

            return announces;
        }

        public struct File
        {
            public long Length { get; set; }
            public string Md5Sum { get; set; }
            public List<string> Path { get; set; }
        }
    }
}
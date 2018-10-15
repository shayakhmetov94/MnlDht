using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using mainline_dht.Base.Bittorrent;

namespace mainline_dht.Base.Message
{
    public partial class MnlMessage
    {
        public class Builder
        {
            private MnlMessage _msg;

            public Builder() {
                _msg = new MnlMessage();
            }

            public Builder(byte[] msg) {
                Bencoder bencoder = new Bencoder();
                var decodedMsg = bencoder.DecodeElement(msg);
                Dictionary<string, object> msgFields = (Dictionary<string, object>)decodedMsg;
                _msg = new MnlMessage();

                try {
                    _msg.TranId = Bencoder.DefEncoding.GetString((byte[])msgFields["t"]);
                } catch(KeyNotFoundException) {
                    throw new MalformedPacketException("t");
                }
                try {
                    _msg.Type = (MessageType)(Bencoder.DefEncoding.GetString((byte[])msgFields["y"])[0]);
                } catch(KeyNotFoundException) {
                    throw new MalformedPacketException("y");
                }

                if(msgFields.ContainsKey("q")) {
                    _msg.QueryType = Bencoder.DefEncoding.GetString((byte[])msgFields["q"]);
                }

                switch(_msg.Type) {
                case MessageType.Query:
                    try {
                        _msg.Payload = (Dictionary<string, object>)msgFields["a"];
                    } catch(KeyNotFoundException) {
                        throw new MalformedPacketException("a");
                    }
                    break;
                case MessageType.Response:
                    try {
                        _msg.Payload = (Dictionary<string, object>)msgFields["r"];
                    } catch(KeyNotFoundException) {
                        throw new MalformedPacketException("r");
                    }
                    break;
                case MessageType.Error:
                    try {
                        List<object> errList = (List<object>)msgFields["e"];
                        int codeIdx = errList[0] is int ? 0 : 1;
                        _msg.ErrorCode = (ErrorType)errList[codeIdx];
                        if(errList.Count > 1) {
                            _msg.ErrorMessage = Bencoder.DefEncoding.GetString((byte[])errList[codeIdx == 0 ? 1 : 0]); //TODO: ???
                        }
                    } catch(KeyNotFoundException) {
                        throw new MalformedPacketException("e");
                    }
                    return;
                }

                _msg.OriginatorId = new Id((byte[])_msg.Payload["id"]);

            }

            public Builder SetTranId(string tranId) {
                if(tranId == null) {
                    throw new ArgumentException("tranId");
                }

                _msg.TranId = tranId;

                return this;
            }

            public Builder SetType(MessageType messageType) {
                _msg.Type = messageType;

                return this;
            }

            public Builder SetQueryType(string queryType) {
                if(queryType == null) {
                    throw new ArgumentNullException("queryType");
                }
                
                _msg.QueryType = queryType;

                return this;
            }

            public Builder SetVersion(string version) {
                if(version == null || version.Length != 2) {
                    throw new ArgumentException("version");
                }

                _msg.OriginatorVersion = version;

                return this;
            }

            public Builder SetOriginatorId(Id originatorId) {
                if(originatorId == null) {
                    throw new ArgumentNullException("originatorId");
                }

                _msg.Payload.Add("id", originatorId.Value);

                return this;
            }

            public Builder SetInfohash(Id infohash) {
                if(infohash == null) {
                    throw new ArgumentNullException("originatorId");
                }

                _msg.Payload.Add("info_hash", infohash.Value);

                return this;
            }

            public Builder SetErrorCode(ErrorType errCode) {
                _msg.ErrorCode = errCode;

                return this;
            }

            public Builder SetErrorMessage(string errMsg) {
                if(string.IsNullOrEmpty(errMsg)) {
                    throw new ArgumentException("errMsg");
                }

                _msg.ErrorMessage = errMsg;

                return this;
            }

            public MnlMessage Build() {
                return _msg;
            }

        }
    }
}

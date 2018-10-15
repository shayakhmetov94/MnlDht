using mainline_dht.Base.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Message
{ 
    public enum MessageType { Query = 'q', Response = 'r', Error = 'e' }
    public enum ErrorType { GenericError = 201, ServerError = 202,  ProtocolError = 203, MethodUnknown = 204 }

    public static class QueryType {
        public const string Ping           = "ping";
        public const string FindNode       = "find_node";
        public const string GetPeers       = "get_peers";
        public const string AnnouncePeer   = "announce_peer";
    }

    public delegate void NodeMessageResponseHandler(MnlMessage response);

    public partial class MnlMessage
    {
        public string TranId { get; set; }
        public MessageType Type { get; private set; }
        public string QueryType { get; private set; }
        public Id OriginatorId { get; private set; }
        public string OriginatorVersion { get; private set; }


        public ErrorType ErrorCode { get; private set; }
        public string ErrorMessage { get; private set; }

        public Dictionary<string, object> Payload { get; private set; }

        public MnlMessage Response { get; private set; }

        private event NodeMessageResponseHandler OnResponse;

        public MnlMessage() {
            Payload = new Dictionary<string, object>();
        }

        public void AddCallback( NodeMessageResponseHandler handler ) {
            if(handler != null)
                OnResponse += handler;
        }

        public void SetResponse(MnlMessage response) {
            Response = response;
            OnResponse?.Invoke(response);
        }

        public byte[] ToBytes() {
            Dictionary<string, object> msgFields = new Dictionary<string, object>();
            msgFields.Add("t", TranId);
            msgFields.Add("y", ((char)Type).ToString());

            if(QueryType != null) {
                msgFields.Add("q", QueryType);
            }

            string argsType = null;
            switch(Type) {
            case MessageType.Query:
                argsType = "a";
                break;
            case MessageType.Response:
                argsType = "r";
                break;
            case MessageType.Error:
                argsType = "e";
                break;
            default:
                throw new NotSupportedException(Type.ToString());
            }

            msgFields.Add(argsType, Payload.OrderBy((v) => v.Key).ToList());

            Bittorrent.Bencoder bencoder = new Bittorrent.Bencoder();
            return bencoder.EncodeElement(msgFields.OrderBy((v) => v.Key).ToList());
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using mainline_dht.Base.Buckets;
using System.Net;
using mainline_dht.Base.Message;
using mainline_dht.Base.Storage;
using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;
using mainline_dht.Base.Storage.Token;

namespace mainline_dht.Base
{
    public class Node {
        public const int NodesPerQuery = 8;    

        private NodeServer _server;
        private TokenStorage _tokenStorage;
        private bool _validateMsgs;

        public BucketList BucketList { get; private set; }
        public Id Id { get; }
        public IInfoHashStorage Storage { get; private set; }
        public IPEndPoint EndPoint {
            get {
                return _server.EndPoint;
            }
        }

        public int TimeoutInMSec { get; set; } = 500;
        public int ValueExpirationInSec { get; private set; } = 86400;

        public Node( IPEndPoint ipEndpoint, bool validateMsgs = true ) {
            Id = Id.GenerateRandom();
            _validateMsgs = validateMsgs;
            InitNode( ipEndpoint );

        }

        public Node( IPEndPoint ipEndpoint, Id id ) {
            Id = id;
            InitNode( ipEndpoint );
        }

        private void InitNode( IPEndPoint ipEndpoint ) {
            _server = new NodeServer(ipEndpoint);
            _server.OnMaliciousMessage += CheckPossibleMaliciousMessage;
            _tokenStorage = new TokenStorage();
            BucketList = new BucketList( Id, 20 );
            Storage = new MemoryInfoHashStorage( 20 );
            _server.Start( this );
        }

        #region RPC API

        /// <summary>
        /// Pings other node 
        /// </summary>
        /// <param name="id">Node id to ping</param>
        /// <returns></returns>
        public MnlMessage Ping( Id id ) {
            var bucket = BucketList.GetBucket(id);
            return Ping( bucket.GetNode( id ) );
        }

        /// <summary>
        /// Pings other node
        /// </summary>
        /// <param name="node">Node to ping</param>
        /// <returns></returns>
        public MnlMessage Ping( ContactNode node ) {
            Message.MnlMessage msg = new Message.MnlMessage.Builder()
                              .SetType(MessageType.Query)
                              .SetQueryType(QueryType.Ping)
                              .SetOriginatorId(Id)
                              .Build();

            UpdateLastSeen(node);
            return _server.SendMessageSync(node, msg, _validateMsgs, TimeoutInMSec ).Response;
        }

        /// <summary>
        /// Pings other node
        /// </summary>
        /// <param name="node">Node to ping</param>
        /// <returns></returns>
        public MnlMessage Ping(IPEndPoint nodeEndPoint) {
            Message.MnlMessage msg = new Message.MnlMessage.Builder()
                              .SetType(MessageType.Query)
                              .SetQueryType(QueryType.Ping)
                              .SetOriginatorId(Id)
                              .Build();
            //do not validate id if we don't know nothing about node except it's address
            return _server.SendMessageSync(new ContactNode(Id, nodeEndPoint), msg, false, TimeoutInMSec).Response;
        }

        /// <summary>
        /// Find contact's closest nodes to id
        /// </summary>
        /// <param name="node">Node to query</param>
        /// <param name="id">Id to compare</param>
        /// <returns>Closest nodes to id</returns>
        public IEnumerable<ContactNode> FindNode(Id id, Id idToFind) {
            var bucket = BucketList.GetBucket(id);
            return FindNode(bucket.GetNode(id), idToFind);
        }

        /// <summary>
        /// Find contact's closest nodes to id
        /// </summary>
        /// <param name="node">Node to query</param>
        /// <param name="id">Id to compare</param>
        /// <returns>Closest nodes to id</returns>
        public IEnumerable<ContactNode> FindNode( ContactNode node, Id id ) {
            Message.MnlMessage msg = new Message.MnlMessage.Builder()
                              .SetType(MessageType.Query)
                              .SetQueryType(QueryType.FindNode)
                              .SetOriginatorId(Id)
                              .Build();

            msg.Payload.Add("target", id.Value);
            Message.MnlMessage response = _server.SendMessageSync(node, msg, _validateMsgs, TimeoutInMSec).Response;
            List<ContactNode> nodes = new List<ContactNode>();

            if(response == null) {
                return nodes;
            }

            UpdateLastSeen(node);

            if(response.Payload.ContainsKey("nodes")) {
                byte[] nodeString = (byte[])response.Payload["nodes"];
                int offset = 0;
                while(offset < nodeString.Length) {
                    nodes.Add(new ContactNode(nodeString, ref offset));
                }
            }

            return nodes;
        }

        /// <summary>
        /// Find contact's closest nodes to id
        /// </summary>
        /// <param name="node">Node to query</param>
        /// <param name="id">Id to compare</param>
        /// <returns>Closest nodes to id</returns>
        public bool GetPeers(ContactNode node, Id infohash, out IEnumerable<ContactNode> nodes, out object token) {
            Message.MnlMessage msg = new Message.MnlMessage.Builder()
                              .SetType(MessageType.Query)
                              .SetQueryType(QueryType.GetPeers)
                              .SetOriginatorId(Id)
                              .SetInfohash(infohash)
                              .Build();

            UpdateLastSeen(node);

            Message.MnlMessage response = _server.SendMessageSync(node, msg, _validateMsgs, TimeoutInMSec).Response;
            List<ContactNode> contacts = new List<ContactNode>();
            nodes = contacts;
            token = null;

            if(response == null) {
                return false;
            }

            if(response.Payload.ContainsKey("token")) {
                token = response.Payload["token"];
            }

            if(response.Payload.ContainsKey("values")) {
                List<object> valuesStrings = (List<object>)response.Payload["values"];
                
                foreach(object peerInfo in valuesStrings) { 
                    contacts.Add(ContactNode.FromPeerInfo(peerInfo as byte[]));
                }

                return true;
            } else if(response.Payload.ContainsKey("nodes")) {
                byte[] nodeString = (byte[])response.Payload["nodes"];
                int offset = 0;
                while(offset < nodeString.Length) {
                    contacts.Add(new ContactNode(nodeString, ref offset));
                }

                return false;
            }
            

            return false;
        }

        /// <summary>
        /// Find contact's closest nodes to id
        /// </summary>
        /// <param name="node">Node to query</param>
        /// <param name="id">Id to compare</param>
        /// <returns></returns>
        public MnlMessage AnnouncePeer(ContactNode node, Id infohash, int port, object token, int? impliedPort) {
            Message.MnlMessage msg = new Message.MnlMessage.Builder()
                              .SetType(MessageType.Query)
                              .SetQueryType(QueryType.AnnouncePeer)
                              .SetOriginatorId(Id)
                              .SetInfohash(infohash)
                              .Build();

            msg.Payload.Add("port", port);
            msg.Payload.Add("implied_port", impliedPort ?? 0);
            msg.Payload.Add("token", token ?? new byte[0]);

            UpdateLastSeen(node);

            return _server.SendMessageSync(node, msg, _validateMsgs, TimeoutInMSec).Response;
        }

        /// <summary>
        /// Shutdown node
        /// </summary>
        public void Shutdown() {
            _server.Stop( true );
        }

        private void UpdateLastSeen( ContactNode node ) {
            var bucket = BucketList.GetBucket(node.Id);
            if ( bucket.Contains(node.Id) ) {
                bucket.SeenNow( node );
            }
        }

        #endregion

        public bool PingAndPutNewContact(IPEndPoint contactEp) {
            if(contactEp == null) {
                throw new ArgumentNullException("contactEp");
            }

            var pingMsg = Ping(contactEp);

            if(pingMsg == null) {
                return false;
            }

            Id contactId = new Id((byte[])pingMsg.Payload["id"]);

            return BucketList.Put(new ContactNode(contactId, contactEp)) == BucketList.BucketPutResult.Success;
        }

        /// <summary>
        /// Processes request from other node. Used by NodeServer class
        /// </summary>
        /// <param name="msg">Request to process</param>
        /// <param name="origAddress">Request originator adress</param>
        public void ProcessQuery(Message.MnlMessage msg, IPEndPoint origAddress ) {
            switch(msg.QueryType) {
                case QueryType.Ping: {
                    _server.SendMessage(origAddress, PrepareResponse(msg));
                    break;
                }

                case QueryType.FindNode: {
                    Message.MnlMessage response = PrepareResponse(msg);

                    Id targetId = new Id((byte[])msg.Payload["target"]); 
                    var closestNodes = BucketList.GetClosestNodes(targetId, NodesPerQuery);
                    response.Payload.Add("nodes", closestNodes.Select((n) => n.ToBytes()).ToList());

                    _server.SendMessage(origAddress, response);
                    break;
                }

                case QueryType.GetPeers: {
                    Message.MnlMessage response = PrepareResponse(msg);

                    Id targetId = new Id((byte[])msg.Payload["info_hash"]); 

                    if(Storage.ContainsPeersFor(targetId)) {
                        response.Payload.Add("values", Storage.GetForinfoHash(targetId)
                                                        .Select((n) => n.ToBytes(true)).ToList());
                        response.Payload.Add("token", _tokenStorage.AcquireNewToken(origAddress.Address).AsBytes());
                    } else {
                        response.Payload.Add("nodes", BucketList.GetClosestNodes(targetId, NodesPerQuery)
                                                        .Select((n) => n.ToBytes()).ToList());
                    }

                    _server.SendMessage(origAddress, response);
                    break;
                }

                case QueryType.AnnouncePeer: {
                    Message.MnlMessage response = PrepareResponse(msg);

                    Token token = Token.FromBytes((byte[])msg.Payload["token"]);    
                    if(_tokenStorage.Validate(origAddress.Address, token)) {
                        Id infoHash = new Id((byte[])msg.Payload["info_hash"]);
                        Id originatorId = new Id((byte[])msg.Payload["id"]);
                        Bucket originatorBucket = BucketList.GetBucket(originatorId);
                        ContactNode originatorNode = originatorBucket.GetNode(originatorId);
                        bool isImpliedPort = !msg.Payload.ContainsKey("port") || (int)msg.Payload["port"] > 0;
                        originatorNode.UtpPort = isImpliedPort ? originatorNode.EndPoint.Port : (int)msg.Payload["port"];

                        Storage.Put(originatorNode, infoHash);

                        _server.SendMessage(origAddress, response);
                    } else {
                        _server.SendBadToken(origAddress);
                    }
                    
                    break;
                }
                
            }

            Id msgOrigId = new Id((byte[])msg.Payload["id"]);

            var bucket = BucketList.GetBucket(msgOrigId);
            //UpdateLastSeen(bucket.GetNode(msgOrigId));
        }

        private MnlMessage PrepareResponse(Message.MnlMessage toMsg) {
            return new Message.MnlMessage.Builder()
                   .SetTranId(toMsg.TranId)
                   .SetType(MessageType.Response)
                   .SetOriginatorId(Id)
                   .Build();
        }

        protected bool CheckPossibleMaliciousMessage(IPEndPoint originator, Id originatorId, MnlMessage maliciousMsg) {
            var malciousNodes = BucketList.GetClosestNodes(originatorId, 8);
            foreach(var maliciousNode in malciousNodes) {
                if(Ping(maliciousNode) == null) {
                    if(BucketList.GetBucket(maliciousNode.Id).Remove(maliciousNode)) {
                        Debug.WriteLine($"Removed malicious node {maliciousNode} from bucket list");
                    } else {
                        Debug.WriteLine($"Can't remove malicious node {maliciousNode} from bucket list");
                    }
                }
            }

            return true;
        }
    }
}

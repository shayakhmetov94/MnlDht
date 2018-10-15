using mainline_dht.Base.Message;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace mainline_dht.Base
{//TODO: process message only from known nodes
    class NodeServer
    {
        #region Events 
        public delegate bool MaliciousMessageHandler(IPEndPoint originator, Id originatorId, MnlMessage maliciousMsg);
        public event MaliciousMessageHandler OnMaliciousMessage;
        #endregion

        private int _currentSeq;
        private ConcurrentDictionary<ulong, PendingMnlMessage> _messages;

        private bool _stopped;
        private Thread _listenerThread;
        private UdpClient _udpClient;

        public IPEndPoint EndPoint { get; private set; }

        public NodeServer(IPEndPoint endPoint) {
            _currentSeq = 33;
            EndPoint = endPoint;
            _messages = new ConcurrentDictionary<ulong, PendingMnlMessage>();
            _stopped = false;
        }

        private string GenerateTranId() {
            if(_currentSeq == 99)
                _currentSeq = 1;

            Interlocked.Increment(ref _currentSeq);

            return _currentSeq.ToString().PadLeft(2, '\0');
        }

        private ulong GetMessageId(IPAddress address, ushort seq) {
            ulong messageId = 0;
            byte[] addressBytes = address.GetAddressBytes();
            for(int i = 8; i < 36; i += 8) {
                messageId |= addressBytes[(i / 8) - 1];
                messageId = messageId << i;
            }

            messageId = messageId << 16;
            messageId |= seq;

            return messageId;
        }

        public void Start(Node ownerNode) {
            _udpClient = new UdpClient(EndPoint);
            _listenerThread = new Thread(ListenIncomingObj);
            _listenerThread.Start(ownerNode);
        }

        public void Stop(bool forced) {
            if(forced) {
                _udpClient.Close();
            }

            _stopped = true;
        }

        public MnlMessage SendMessageSync(ContactNode toNode, Message.MnlMessage msg, bool validate, int timeout) {  
            msg.TranId = GenerateTranId();

            ManualResetEventSlim eventReset = new ManualResetEventSlim();
            msg.AddCallback((m) => eventReset.Set());
            ulong msgId = GetMessageId(toNode.EndPoint.Address, BitConverter.ToUInt16(Encoding.ASCII.GetBytes(msg.TranId), 0));
            _messages.AddOrUpdate(msgId,
                new PendingMnlMessage() {
                    RecipientId = toNode.Id,
                    SentMessage = msg,
                    ValidateId = validate
                }, (k, v) => v);

            byte[] rawMsg = msg.ToBytes();

            _udpClient.Send(rawMsg, rawMsg.Length, toNode.EndPoint);

            if(eventReset.Wait(timeout)) {
                PendingMnlMessage msgFromQueue;
                if(_messages.TryRemove(msgId, out msgFromQueue))
                    return msgFromQueue.SentMessage;
            }

            return msg;
        }

        public void SendMessage(IPEndPoint toEndPoint, Message.MnlMessage msg) {
            byte[] rawMsg = msg.ToBytes();
            _udpClient.Send(rawMsg, rawMsg.Length, toEndPoint);
        }

        public void SendBadToken(IPEndPoint toEndPoint) {
            Message.MnlMessage badTokenMsg = new Message.MnlMessage.Builder()
                                      .SetType(MessageType.Error)
                                      .SetErrorCode(ErrorType.ProtocolError)
                                      .SetErrorMessage("Bad token")
                                      .Build();

            SendMessage(toEndPoint, badTokenMsg);
        }

        public void ListenIncomingObj(object ownerNodeObj) {
            ListenIncoming((Node)ownerNodeObj);
        }

        private void ListenIncoming(Node ownerNode) {
            while(!_stopped) {
                try {
                    IPEndPoint incomingIpEndPoint = new IPEndPoint(IPAddress.Any, EndPoint.Port);

                    byte[] rawMsg = _udpClient.Receive(ref incomingIpEndPoint);
                    if(!IsLegitMnlDhtMessage(rawMsg)) {
                        continue;
                    }

                    Message.MnlMessage incomingMsg = null;
                    try {
                        incomingMsg = new Message.MnlMessage.Builder(rawMsg).Build();
                    } catch(Bittorrent.InvalidFieldException ife) {
                        MnlMessage errMessage = new MnlMessage.Builder()
                                                .SetTranId(GenerateTranId())
                                                .SetType(MessageType.Error)
                                                .SetErrorCode(ErrorType.ProtocolError)
                                                .SetErrorMessage(ife.Message)
                                                .Build();

                        Debug.WriteLine($"Sending incorrect field message {ife.Message}");
                        SendMessage(incomingIpEndPoint, errMessage);
                        continue;
                    } catch(MalformedPacketException mpe) {
                        MnlMessage errMessage = new MnlMessage.Builder()
                                                .SetTranId(GenerateTranId())
                                                .SetType(MessageType.Error)
                                                .SetErrorCode(ErrorType.ProtocolError)
                                                .SetErrorMessage(mpe.Message)
                                                .Build();

                        Debug.WriteLine($"Sending malformed packet message {mpe.Message}");
                        SendMessage(incomingIpEndPoint, errMessage);
                        continue;

                    }
                    
                    ulong msgIdx = GetMessageId(incomingIpEndPoint.Address, BitConverter.ToUInt16(Encoding.ASCII.GetBytes(incomingMsg.TranId.PadLeft(2, '\0')), 0));
                    if(_messages.ContainsKey(msgIdx)) {
                        PendingMnlMessage rqMsg;
                        if(_messages.TryRemove(msgIdx, out rqMsg)) {
                            if(incomingMsg.Type == MessageType.Error) {
                                Debug.WriteLine($"Recieved error {incomingMsg.ErrorMessage}");
                                switch(incomingMsg.ErrorCode) {
                                    case ErrorType.ProtocolError:
                                    case ErrorType.MethodUnknown: 
                                        Debug.WriteLine($"!!!Error {incomingMsg.ErrorMessage}");
                                    break;
                                    default:
                                        continue;
                                }
                            }

                            if(rqMsg.ValidateId) {
                                if(rqMsg.RecipientId != incomingMsg.OriginatorId) {
                                    Debug.WriteLine($"Node {incomingIpEndPoint} id mismatch!");
                                    OnMaliciousMessage?.Invoke(incomingIpEndPoint, rqMsg.RecipientId, rqMsg.SentMessage);
                                    continue;
                                }
                            }

                            rqMsg.SentMessage.SetResponse(incomingMsg);
                        }
                    } else {
                        if(incomingMsg.Type == MessageType.Query) {
                            ownerNode.ProcessQuery(incomingMsg, incomingIpEndPoint);
                        }
                    }

                } catch(SocketException se) {
                    if(se.SocketErrorCode != SocketError.Interrupted) {
                        throw;
                    }
                }
            }

        }

        private bool IsLegitMnlDhtMessage(byte[] rawMsg) {
            if(rawMsg[0] == 65) { //TODO: implement http://bittorrent.org/beps/bep_0029.html
                Debug.WriteLine($"NodeServer: recieved uTP ST_SYN");
                return false;
            }

            if(rawMsg[0] != 'd' || rawMsg.Length < 10) {
                return false;
            }

            return true;
        }

        #region Internal structs 
        
        struct PendingMnlMessage {
            public Id RecipientId { get; set; }
            public MnlMessage SentMessage { get; set; }
            public bool ValidateId { get; set; }
        }

        #endregion
    }
}

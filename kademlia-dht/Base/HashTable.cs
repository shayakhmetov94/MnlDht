using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentScheduler;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using mainline_dht.Base.Query;

namespace mainline_dht.Base
{
    public class HashTable
    {
        private Registry _dhtSchedulesRegistry;
        private int _utpPort;
        private CancellationTokenSource _myCancelTokenOwner;

        /// <summary>
        /// Owner node
        /// </summary>
        public Node Owner { get; private set; }

        /// <summary>
        /// System-wide replication parameter
        /// </summary>
        public int ReplicationCount { get; private set; } = 8;

        /// <summary>
        /// System-wide concurrency parameter
        /// </summary>
        public int MaxConcurrentThreads { get; private set; } = 3;

        /// <summary>
        /// Bucket refresh period
        /// </summary>
        public int BucketsRefreshInSecs { get; private set; } = 3600;

        public InfoHashQueryHistory QueryHistory { get; private set; }

        private SortedSet<Id> _ignoredIds; 


        public HashTable(Node node, IEnumerable<IPEndPoint> knownNodes, int utpPort, KadHashTableConfiguration kadHashTableConfig = null, bool useCache = false) {
            Owner = node;
            if(knownNodes != null) {
                foreach(var nodeEp in knownNodes) {
                    Owner.PingAndPutNewContact(nodeEp);
                }
            }

            if(kadHashTableConfig != null) {
                ReplicationCount = kadHashTableConfig.ReplicationCount ?? ReplicationCount;
                MaxConcurrentThreads = kadHashTableConfig.MaxConcurrentThreads ?? MaxConcurrentThreads;
                BucketsRefreshInSecs = kadHashTableConfig.BucketsRefreshInSecs ?? BucketsRefreshInSecs;
            }

            _utpPort = utpPort;
            _ignoredIds = new SortedSet<Id>(new Id.IdComparer());
            _myCancelTokenOwner = new CancellationTokenSource();
            if(useCache) {
                QueryHistory = new InfoHashQueryHistory();
            }

            InitTable();
        }

        private void InitTable() {
            RefreshBucket( Owner.Id );

            _dhtSchedulesRegistry = new Registry();
            
            _dhtSchedulesRegistry.Schedule((Action)RefreshUnaccessedBuckets).ToRunEvery(BucketsRefreshInSecs).Seconds();

            JobManager.Initialize(_dhtSchedulesRegistry);
            JobManager.Start();
        }

        public void RefreshUnaccessedBuckets() {
            SortedSet<Buckets.Bucket> sortedBuckets = new SortedSet<Buckets.Bucket>(Owner.BucketList.Buckets, new Buckets.Bucket.ByLastUpdatedComparer());
            foreach(var bucket in sortedBuckets) {
                if((DateTime.UtcNow - bucket.LastUpdated).TotalSeconds > BucketsRefreshInSecs) {
                    byte[] intBytes = BitConverter.GetBytes(bucket.Id);
                    byte[] idBytes = new byte[20];
                    Buffer.BlockCopy(intBytes, 0, idBytes, 0, intBytes.Length);
                    Id bucketId = new Id(idBytes);
                    RefreshBucket(bucketId);
                } else {
                    break;
                }
            }
        }

        public void RefreshBucket(Id id) {
            LookupNodes(id, _myCancelTokenOwner.Token);

            foreach ( var contactNode in Owner.BucketList.GetClosestNodes(id, ReplicationCount)) {
                switch ( Owner.BucketList.Put( contactNode ) ) {
                    case Buckets.BucketList.BucketPutResult.BucketIsFull: 
                        TryReplaceLeastSeenContactFromBucket( contactNode );
                        break;
                    case Buckets.BucketList.BucketPutResult.Updated:
                    case Buckets.BucketList.BucketPutResult.Success:
                        //Nothing to do here.
                        break;
                }
            }
        }

        public BlockingCollection<ContactNode> FindPeers(Id infoHash, CancellationToken? cancelToken = null) {
            CancellationToken cancellationToken = cancelToken == null ? _myCancelTokenOwner.Token : cancelToken.Value;
            BlockingCollection<ContactNode> peers = new BlockingCollection<ContactNode>();
            Task.Factory.StartNew(InvokePeerLookupObj, new Tuple<Id, BlockingCollection<ContactNode>, CancellationToken>(infoHash, peers, cancellationToken));
            return peers;
        }

        private void InvokePeerLookupObj(object infoHashIdAndPeersAndCancelTknTupleObj) {
            Tuple<Id, BlockingCollection<ContactNode>, CancellationToken> infoHashIdAndPeersAndCancelTkn = (Tuple<Id, BlockingCollection<ContactNode>, CancellationToken>)infoHashIdAndPeersAndCancelTknTupleObj;
            InvokePeerLookup(infoHashIdAndPeersAndCancelTkn.Item1, infoHashIdAndPeersAndCancelTkn.Item2, infoHashIdAndPeersAndCancelTkn.Item3);
        }

        private void InvokePeerLookup(Id infoHash, BlockingCollection<ContactNode> peers, CancellationToken cancelToken) {
            LookupNodes(infoHash, cancelToken);
            if(cancelToken.IsCancellationRequested) {
                return;
            }

            LookupPeers(infoHash, peers, cancelToken);
        }

        public bool TryReplaceLeastSeenContactFromBucket(ContactNode newContact) {
            var bucket = Owner.BucketList.GetBucket( newContact.Id);
            var leastSeen = bucket.GetLeastSeen();

            Message.MnlMessage pongMsg = Owner.Ping( leastSeen );

            if ( pongMsg == null ) {
                bucket.Replace( leastSeen.Id, newContact );
                return true;
            }

            return false;
        }

        private void LookupNodes(Id nodeId, CancellationToken cancelToken) {
            SortedSet<Id> queriedNodes = new SortedSet<Id>(new Id.IdComparer());

            List<Task> tasks = new List<Task>(MaxConcurrentThreads);
            int triesCount = 0;
            ContactNode closestContact = Owner.BucketList.GetClosestContactToId(nodeId);

            if(closestContact == null){
                return;
            }

            Id prevClosestId = closestContact.Id ^ nodeId;

            while(queriedNodes.Count < 256) { 
                var shortListSnapshot = Owner.BucketList.GetClosestNodes(nodeId, MaxConcurrentThreads).Where((n) => !queriedNodes.Contains(n.Id)).ToList();
                foreach(var node in shortListSnapshot) {
                    tasks.Add(Task.Run(() => {
                        IEnumerable<ContactNode> contacts = Owner.FindNode(node, nodeId);
                        foreach(var contact in contacts) {
                            if(Owner.BucketList.Put(contact) == Buckets.BucketList.BucketPutResult.BucketIsFull) {
                                TryReplaceLeastSeenContactFromBucket(contact);
                            }
                        }

                        Debug.WriteLine($"Added {contacts.Count()} contacts");
                    }));

                    queriedNodes.Add(node.Id);
                    
                    if(tasks.Count >= MaxConcurrentThreads) {
                        break;
                    }

                    Debug.WriteLine($"Queried nodes count - {queriedNodes.Count}");
                }
                try {
                    Task.WaitAll(tasks.ToArray(), cancelToken);
                    tasks.Clear();
                } catch(OperationCanceledException) {
                    return;
                }
                ContactNode closestNode = Owner.BucketList.GetClosestContactToId(nodeId);

                Id currentMin = closestNode.Id ^ nodeId;
                if(currentMin >= prevClosestId) {
                    if(triesCount < ReplicationCount) {
                        triesCount++;
                        continue;
                    }

                    //Can't get any closer, stopping.
                    Debug.WriteLine($"Can't get any closer, breaking search");
                    break;
                }

                prevClosestId = currentMin;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        private void LookupPeers(Id infoHash, BlockingCollection<ContactNode> peers, CancellationToken cancelToken) {           
            SortedSet<ContactNode> queriedNodes = new SortedSet<ContactNode>(new ContactNode.IdComparer()),
                                   returnedNodes = new SortedSet<ContactNode>(new ContactNode.IdComparer());

            List<Task> tasks = new List<Task>(MaxConcurrentThreads);
            ConcurrentBag<ContactNode> shortList = null;
            List<ContactNode> shortListBase = new List<ContactNode>(ReplicationCount);
            if(QueryHistory != null) {
                if(QueryHistory.HasEntryFor(infoHash)) {
                    var infoHashEntry = QueryHistory.GetEntryFor(infoHash);
                    if(!infoHashEntry.IsExpired()) {
                        shortListBase.AddRange(infoHashEntry.ReturnedNodes);
                    }
                }
            }

            shortListBase.AddRange(Owner.BucketList.GetClosestNodes(infoHash, MaxConcurrentThreads));

            if(shortListBase.Count == 0) {
                peers.Add(null);
                return;
            }

            shortList = new ConcurrentBag<ContactNode>(shortListBase);

            while(queriedNodes.Count < 256 ) {  
                var shortListSnapshot = shortList.OrderBy((c) => (c.Id ^ infoHash).GetNumericValue()).Where((n) => !queriedNodes.Contains(n)).Take(ReplicationCount).ToList();
                foreach(var node in shortListSnapshot) {
                    tasks.Add(Task.Run(() => {
                        if(!_ignoredIds.Contains(node.Id)) {

                            if(Owner.GetPeers(node, infoHash, out IEnumerable<ContactNode> contacts, out object returnedNodeToken)) {
                                if(contacts.Count() == 0) {
                                    _ignoredIds.Add(node.Id);
                                    return;
                                }   

                                foreach(var peer in contacts) {
                                    peers.Add(peer);
                                }

                                Owner.AnnouncePeer(node, infoHash, _utpPort, returnedNodeToken, 0);

                                lock(returnedNodes) {
                                    returnedNodes.Add(node);
                                }
                            } else {
                                foreach(var contact in contacts) {
                                    shortList.Add(contact);
                                }

                                Debug.WriteLine($"Added {contacts.Count()} contacts");
                            }
                        }
                    }));

                    queriedNodes.Add(node);

                    if(tasks.Count >= MaxConcurrentThreads) {
                        break;
                    }
                    
                    Debug.WriteLine($"Queried nodes count - {queriedNodes.Count}");
                }

                if(tasks.Count == 0) {
                    break;
                }

                try {
                    Task.WaitAll(tasks.ToArray(), cancelToken);
                } catch(OperationCanceledException) {
                    break;
                }

                tasks.Clear();

                //if(returnedNode != null) {
                //    break;
                //}
            }

            peers.Add(null);
            Debug.WriteLine($"LookupPeers is done");

            if(QueryHistory != null) {
                QueryHistory.PutNewOrReplace(new InfoHashQueryHistoryEntry(infoHash, queriedNodes, returnedNodes));
            }

            foreach(var queriedNode in queriedNodes) {
                if(Owner.BucketList.Put(queriedNode) == Buckets.BucketList.BucketPutResult.BucketIsFull) {
                    TryReplaceLeastSeenContactFromBucket(queriedNode);
                }
            }
        }

        public void Shutdown() {
            JobManager.StopAndBlock();
            Owner.Shutdown();
        }
    }
}

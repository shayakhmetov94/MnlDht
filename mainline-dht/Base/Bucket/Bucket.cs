using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Buckets
{
    public class Bucket
    {
        private int _size;
        private Dictionary<Id, BucketContactNode> _nodesMap;
        private object __rwlock = new object();

        public int Id { get; }
        public int NodesCount { get { return _nodesMap.Count; } }
        public DateTime LastUpdated { get; private set; }

        public Bucket(int id, int size) {
            Id = id;
            _size = size;
            _nodesMap = new Dictionary<Id, BucketContactNode>( new Id.EqualityComparer() );
            LastUpdated = DateTime.UtcNow;
        }

        public void Put(ContactNode node) {
            lock(__rwlock) {
                if(IsFull()) {
                    throw new Exception("Bucket is full");
                }

                var newNode = new BucketContactNode(node) { LastUsed = DateTime.UtcNow };
                LastUpdated = DateTime.UtcNow;

                _nodesMap.Add(node.Id, newNode);
            }
        }

        public bool Remove(ContactNode node) {
            lock(__rwlock) {
                return _nodesMap.Remove(node.Id);
            }
        } 

        public bool IsFull() {
            return _size <= _nodesMap.Count;
        }

        public bool Contains(Id id) {
            lock ( __rwlock ) {
                return _nodesMap.ContainsKey( id );
            }
        }

        public ContactNode GetLeastSeen() {
            if(_nodesMap.Count == 0) {
                return null;
            }    

            lock ( __rwlock ) {
                return _nodesMap.Select(kv=>kv.Value).OrderBy((bn)=>bn.LastUsed).First().Node;
            }
        } 

        public void Replace(Id oldId, ContactNode newNode) {
            var added = new BucketContactNode(newNode) { LastUsed = DateTime.UtcNow};
            lock ( __rwlock ) {
                if(!_nodesMap.ContainsKey(oldId)) {
                    return;
                }

                var replaced = _nodesMap[oldId];
                _nodesMap.Remove( oldId );
                _nodesMap[added.Node.Id] = added;
            }
        }

        public void SeenNow( ContactNode node ) {
            Replace( node.Id, node );
        }

        public ContactNode GetNode(Id id) {
            return _nodesMap[id].Node;
        }
        public IEnumerable<ContactNode> GetNodes() {
            return GetNodes( NodesCount );
        }

        public IEnumerable<ContactNode> GetNodes( int count ) {//TODO: needs to be indexed
            return _nodesMap.Select(kv => kv.Value)
                            .OrderBy(bn => bn.LastUsed)
                            .Select((bn) => bn.Node)
                            .ToList();
        }

        class BucketContactNode
        {
            public ContactNode Node { get; }
            public DateTime LastUsed { get; set; }

            public BucketContactNode(ContactNode node) {
                Node = node;
            }

            public class BucketContactComparer : IComparer<BucketContactNode>, IEqualityComparer<BucketContactNode>
            {
                public int Compare( BucketContactNode x, BucketContactNode y ) {
                    if(x.Node.Id.GetNumericValue() == y.Node.Id.GetNumericValue())
                        return 0;

                    if(x.LastUsed.Equals(y.LastUsed))
                        return -1;

                    return x.LastUsed.CompareTo( y.LastUsed);
                }

                public bool Equals( BucketContactNode x, BucketContactNode y ) {
                    return x.Node.Id.GetNumericValue().Equals( y.Node.Id.GetNumericValue() );
                }

                public int GetHashCode( BucketContactNode obj ) {
                    return obj.Node.Id.GetNumericValue().GetHashCode();
                }
            }

        }

        public class ByLastUpdatedComparer : IComparer<Bucket>
        {
            public int Compare( Bucket x, Bucket y ) {
                return x.LastUpdated.CompareTo( y );
            }
        }
    }
}

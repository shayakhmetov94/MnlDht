using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Buckets
{
    //TODO: implement https://github.com/the8472/mldht/blob/sanitizing-docs/docs/sanitizing-algorithms.rst
    public class BucketList
    {
        public enum BucketPutResult { Success, Updated, BucketIsFull }

        private Id currentNodeId;
        private int k;
        private ConcurrentDictionary<int, Bucket> _buckets;
        private ConcurrentDictionary<int, DateTime> _bucket2RefreshDate;

        public IEnumerable<Bucket> Buckets {
            get {
                return _buckets.Values.ToList();
            }
        }

        public BucketList(Id nodeId, int nodeCountInBucket) {
            currentNodeId = nodeId;
            k = nodeCountInBucket;
            _buckets = new ConcurrentDictionary<int, Bucket>();
            _bucket2RefreshDate = new ConcurrentDictionary<int, DateTime>();
        }

        public BucketPutResult Put(ContactNode node) {
            var bucket = GetBucket(node.Id);

            if ( bucket.IsFull() )
                return BucketPutResult.BucketIsFull;

            if ( bucket.Contains( node.Id ) ) {
                bucket.SeenNow( node );
                return BucketPutResult.Updated;
            }

            bucket.Put( node );

            return BucketPutResult.Success;
        }

        public Bucket GetBucket(Id forId) {
            int bucketId = GetBucketIdForKadId(forId);

            if ( _buckets.ContainsKey( bucketId ) ) 
                return _buckets[bucketId];

            return CreateNewBucket( bucketId );
        }

        public IEnumerable<ContactNode> GetClosestNodes(Id closeTo, int count) {
            int bucketId = GetBucketIdForKadId(closeTo);
            var orderedKeys = _buckets.Keys.OrderBy( ( t ) => Math.Abs(t - bucketId)).ToList();
            List<ContactNode> closests = new List<ContactNode>();

            foreach (var bucketKey in orderedKeys ) {
                foreach ( var node in _buckets[bucketKey].GetNodes( count - closests.Count ) )
                    closests.Add( node );

                if ( closests.Count == count )
                    break;
            }

            return closests;
        }

        public ContactNode GetClosestContactToId(Id id, int depth = 8) {
            var closests = GetClosestNodes(id, depth);
            if(closests.Count() > 0) {
                return closests.Aggregate((p, n) => (p.Id ^ id) < (n.Id ^ id) ? p : n);
            }

            return null;
        }

        private Bucket CreateNewBucket(int idx) {
            var bucket = new Bucket(idx, k);
            _buckets.AddOrUpdate( idx, bucket, ( k, v ) => v );
            return bucket;
        }

        private int GetBucketIdForKadId(Id id) {
            BigInteger distance = (id ^ currentNodeId).GetNumericValue();
            return (int)BigInteger.Log(distance, 2);
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Storage
{
    class MemoryInfoHashStorage : IInfoHashStorage
    {
        private int _maxSize;
        private ConcurrentDictionary<Id, SortedSet<ContactNode>> _infoHash2Nodes;

        public MemoryInfoHashStorage(int maxSize) {
            _maxSize = maxSize;
            _infoHash2Nodes = new ConcurrentDictionary<Id, SortedSet<ContactNode>>(new Id.EqualityComparer());
        }

        public int Count() {
            return _infoHash2Nodes.Select((kv) => kv.Value).Sum((l) => l.Count);
        }

        public bool Delete(Id infoHash, ContactNode contact) {
            SortedSet<ContactNode> peersSet = null;
            if(_infoHash2Nodes.TryGetValue(infoHash, out peersSet)) {
                lock(peersSet) {
                    peersSet.Remove(contact);
                }

                return true;
            }

            return false;
        }

        public IEnumerable<ContactNode> GetForinfoHash(Id infoHash) {
            SortedSet<ContactNode> peersSet = null;
            if(_infoHash2Nodes.TryGetValue(infoHash, out peersSet)) {
                return peersSet.ToList();
            }

            return new List<ContactNode>();
        }

        public int GetMaxSize() {
            return _maxSize;
        }

        public bool Put(ContactNode contact, Id infoHash) {
            if(_infoHash2Nodes.ContainsKey(infoHash)) {
                SortedSet<ContactNode> peersSet = null;
                if(_infoHash2Nodes.TryGetValue(infoHash, out peersSet)) {
                    lock(peersSet) {
                        peersSet.Add(contact);
                    }
                }
            } else {
                SortedSet<ContactNode> peersSet = new SortedSet<ContactNode>(new ContactNode.IdComparer());
                peersSet.Add(contact);
                _infoHash2Nodes.AddOrUpdate(infoHash, peersSet, (nv, ov) => ov);
            }

            return true;
        }

        public bool ContainsPeersFor(Id infoHash) {
            SortedSet<ContactNode> peersSet = null;
            if(_infoHash2Nodes.TryGetValue(infoHash, out peersSet)) {
                return peersSet.Count > 0;
            }

            return false;
        }
    }
}

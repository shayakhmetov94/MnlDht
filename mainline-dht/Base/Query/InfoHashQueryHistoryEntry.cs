using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Query
{
    public class InfoHashQueryHistoryEntry
    {
        public const int ExpirationInSecs = 300;

        public Id InfoHash { get; private set; }
        public SortedSet<ContactNode> QueriedNodes { get; private set; }
        public SortedSet<ContactNode> ReturnedNodes { get; private set; }

        public DateTime UtcStoreTime { get; private set; }

        public InfoHashQueryHistoryEntry(Id infoHash, SortedSet<ContactNode> queriedNodes, SortedSet<ContactNode> returnedNodes) {
            this.InfoHash = infoHash;
            this.QueriedNodes = queriedNodes;
            this.ReturnedNodes = ReturnedNodes;

            UtcStoreTime = DateTime.UtcNow;
        }

        public bool IsExpired() {
            return (DateTime.UtcNow - UtcStoreTime).TotalSeconds > ExpirationInSecs;
        }
    }
}

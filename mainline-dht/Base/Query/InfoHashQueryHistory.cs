using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Query
{
    public class InfoHashQueryHistory
    {
        private ConcurrentDictionary<Id, InfoHashQueryHistoryEntry> _entries;
        
        public InfoHashQueryHistory() {
            _entries = new ConcurrentDictionary<Id, InfoHashQueryHistoryEntry>();
        }

        public bool HasEntryFor(Id infoHash) {
            return _entries.ContainsKey(infoHash);
        }
        
        public InfoHashQueryHistoryEntry GetEntryFor(Id infoHash) {
            return _entries[infoHash];
        }

        public void PutNewOrReplace(InfoHashQueryHistoryEntry entry) {
            _entries.AddOrUpdate(entry.InfoHash, entry, (k, v) => v);
        }
    }
}

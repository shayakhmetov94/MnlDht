using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base
{
    public class KadHashTableConfiguration
    {
        public int? ReplicationCount { get; set; }
        public int? MaxConcurrentThreads { get; set; }
        public int? BucketsRefreshInSecs{ get; set; }

        public static KadHashTableConfiguration ForKadHashTable(HashTable hashTable){
            KadHashTableConfiguration tableState = new KadHashTableConfiguration();

            tableState.ReplicationCount = hashTable.ReplicationCount;
            tableState.MaxConcurrentThreads = hashTable.MaxConcurrentThreads;
            tableState.BucketsRefreshInSecs = hashTable.BucketsRefreshInSecs;

            return tableState;
        }
    }
}

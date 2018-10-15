using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Storage
{
    public interface IInfoHashStorage
    {
        int GetMaxSize();
        int Count();
        
        bool Put(ContactNode contact, Id infoHash);
        IEnumerable<ContactNode> GetForinfoHash(Id infoHash);
        bool Delete(Id infoHash, ContactNode contact);
        bool ContainsPeersFor(Id infohash);
    }
}

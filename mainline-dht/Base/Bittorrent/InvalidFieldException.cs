using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Bittorrent
{
    [Serializable]
    class InvalidFieldException : Exception {
        private string _fieldName;

        public override string Message => $"Field '{_fieldName}' is invalid"; 

        public InvalidFieldException(string fieldName) {
            _fieldName = fieldName;
        }
    }
}

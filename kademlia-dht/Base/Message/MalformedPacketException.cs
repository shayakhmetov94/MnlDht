using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mainline_dht.Base.Message
{
    class MalformedPacketException : Exception
    {
        private string _fieldName;

        public override string Message => $"Malformed message. Field {_fieldName} is absent or invalid.";

        public MalformedPacketException(string fieldName) {
            _fieldName = fieldName;
        }
    }
}

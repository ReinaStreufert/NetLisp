using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Structs
{
    public struct ExtendedTypeInfo
    {
        public Guid ExtendedTypeGuid { get; set; } // may be safely used to check extended type
        public string ExtendedTypeName { get; set; } // expect this field to have collisions and use only for error messages
    }
}

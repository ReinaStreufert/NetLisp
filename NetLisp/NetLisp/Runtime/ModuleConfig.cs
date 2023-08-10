using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    class ModuleConfig
    {
        public string SymbolName { get; set; }
        public string FullName { get; set; }
        public List<SourceInfo> SourceChain { get; set; } = new List<SourceInfo>();
    }
    class SourceInfo
    {
        public bool IsNativeSource { get; set; }
        public string Path { get; set; }
        public string InnerType { get; set; }
    }
}

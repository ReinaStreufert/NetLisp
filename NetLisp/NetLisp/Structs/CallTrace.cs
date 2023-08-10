using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Structs
{
    public class CallTrace
    {
        public SourceReference CallerLocation { get; set; }
        public ExecutableLispToken CalledToken { get; set; }
        public bool LoopFlag { get; set; } = false;
        public LispList LoopCallTarget { get; set; } = null;
        public CallTrace(SourceReference callerLocation, ExecutableLispToken calledToken)
        {
            CallerLocation = callerLocation;
            CalledToken = calledToken;
        }
    }
}

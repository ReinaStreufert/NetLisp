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
        public SourceReference CallerLocation;
        public ExecutableLispToken CalledToken;
        public CallTrace(SourceReference callerLocation, ExecutableLispToken calledToken)
        {
            CallerLocation = callerLocation;
            CalledToken = calledToken;
        }
    }
}

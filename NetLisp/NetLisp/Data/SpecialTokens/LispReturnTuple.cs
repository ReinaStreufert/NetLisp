using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data.SpecialTokens
{
    public class LispReturnTuple : SpecialLispToken
    {
        public List<LispToken> ReturnValues { get; set; } = new List<LispToken>();

        public override LispSpecialType SpecialType => LispSpecialType.ReturnTuple;

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            runtimeContext.RaiseRuntimeError(this, RuntimeErrorType.SpecialTokenMisuse, "'returnvalues' must be used only when returning from a function or scope.");
            yield break;
        }
    }
}

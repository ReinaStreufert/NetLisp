using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data.SpecialTokens
{
    public class LispImmediateReturnToken : SpecialLispToken
    {
        public override LispSpecialType SpecialType => LispSpecialType.ImmediateReturn;
        public override bool IsImmediateReturnToken => true;

        public ExecutableLispToken ReturnFrom { get; set; }
        public List<LispToken> ReturnValues { get; set; }

        public LispImmediateReturnToken(ExecutableLispToken returnFrom, List<LispToken> returnValues)
        {
            ReturnFrom = returnFrom;
            ReturnValues = returnValues;
        }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            runtimeContext.RaiseRuntimeError(this, RuntimeErrorType.SpecialTokenMisuse, "'return-from' should be used to return from a function but is used like a value");
            yield break;
        }
    }
}

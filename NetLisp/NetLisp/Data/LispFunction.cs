using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispFunction : ArgumentDefinedLispRoutine
    {
        // this line is the only effective difference between a LispFunction and a LispMacro.
        // it tells the evaluator to evaluate its arguments before handing controle to Execute.
        // and tells it NOT to re-evaluate the return values.
        public override LispDataType Type => LispDataType.Function;

        public LispFunction(ExecutableBody functionBody, params LispSymbol[] arguments)
        {
            Body = functionBody;
            Arguments = arguments.ToList();
        }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }

        public override string ToString()
        {
            return Text.LispTokenWriter.FunctionToString;
        }
    }
}

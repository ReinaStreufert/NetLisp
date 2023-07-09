using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispMacro : ArgumentDefinedLispRoutine
    {
        // this line is the only effective difference between a LispMacro and a LispFunction.
        // it tells the evaluator NOT to evaluate its arguments before handing controle to Execute
        // and tells it to re-evaluate the return values.
        public override LispDataType Type => LispDataType.Macro;

        public LispMacro(ExecutableBody functionBody, params LispSymbol[] arguments)
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
            return Text.LispTokenWriter.MacroToString;
        }
    }
}

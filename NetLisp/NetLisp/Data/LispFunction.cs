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
        private ScopeStack lambdaScope;

        public override LispDataType Type => LispDataType.Function;

        public LispFunction(ExecutableBody functionBody, ScopeStack executingScope, ArgumentDefinedMetadata? metadata, params LispSymbol[] arguments)
        {
            Body = functionBody;
            Arguments = arguments;
            if (metadata != null)
            {
                InstanceMetadata = metadata;
            } else
            {
                InstanceMetadata = ArgumentDefinedMetadata.CreateBlank(arguments);
            }
            lambdaScope = executingScope;
        }

        protected override ScopeStack GetExecutingScope(RuntimeContext runtimeContext)
        {
            return lambdaScope;
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

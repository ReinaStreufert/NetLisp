using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    static class FlowNativeMacros
    {
        public static LispMacro CreateIfMacro()
        {
            NativeExecutableBody macroBody = new NativeExecutableBody((RuntimeContext runtimeContext) =>
            {
                LispToken condition = runtimeContext.Scopes.CurrentScope.Get("condition");
                LispToken texpr = runtimeContext.Scopes.CurrentScope.Get("texpr");
                LispToken fexpr = runtimeContext.Scopes.CurrentScope.Get("fexpr");

                LispToken evaluatedCondition = null;
                IEnumerable<LispToken> evaluationResult = condition.Evaluate(runtimeContext);
                foreach (LispToken token in evaluationResult)
                {
                    if (evaluatedCondition == null)
                    {
                        evaluatedCondition = token;
                    } else
                    {
                        runtimeContext.RaiseRuntimeError(condition, RuntimeErrorType.ExpectedSingleValue, "Condition evaluated to multiple values");
                    }
                }
                if (evaluatedCondition == null)
                {
                    runtimeContext.RaiseRuntimeError(condition, RuntimeErrorType.ExpectedSingleValue, "Condition evaluated to no values");
                }
                if (evaluatedCondition.Type != LispDataType.Boolean)
                {
                    runtimeContext.RaiseRuntimeError(condition, RuntimeErrorType.ArgumentMismatchError, "Expected boolean after evaluation. Got " + evaluatedCondition.Type.ToString().ToLower());
                }
                bool conditionValue = ((LispBoolean)evaluatedCondition).Value;
                if (conditionValue)
                {
                    return new LispToken[1] { texpr };
                } else
                {
                    return new LispToken[1] { fexpr };
                }
            });
            return new LispMacro(macroBody, new LispSymbol("condition"), new LispSymbol("texpr"), new LispSymbol("fexpr"));
        }
    }
}

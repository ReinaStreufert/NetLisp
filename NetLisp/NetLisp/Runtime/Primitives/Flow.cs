using NetLisp.Data;
using NetLisp.Data.SpecialTokens;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    static class FlowNativeMacros
    {
        public static LispMacro CreateIfMacro(Scope globalScope)
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
                bool conditionValue = (runtimeContext.Assert<LispBoolean>(evaluatedCondition, LispDataType.Boolean)).Value;
                if (conditionValue)
                {
                    return new LispToken[1] { texpr };
                } else
                {
                    return new LispToken[1] { fexpr };
                }
            });
            return new LispMacro(macroBody, ScopeStack.ConstructFromScope(globalScope), null, new LispSymbol("condition"), new LispSymbol("texpr"), new LispSymbol("fexpr"));
        }
    }
    class Runitback : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            // not used
            yield break;
        }
        protected override IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext)
        {
            if (!runtimeContext.Calls.SetLoopFlag(target))
            {
                runtimeContext.RaiseRuntimeError(target, RuntimeErrorType.CannotRunitback, "Searched entire call stack and found no suitable functions to loop");
            }
            yield break;
        }
    }
    class ReturnFrom : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count < 1 || passedArgs.Count > 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "'return-from' takes 1 or 2 arguments");
            }
            LispFunction returnFromFunction = runtimeContext.Assert<LispFunction>(passedArgs[0], LispDataType.Function);
            bool functionInStack = false;
            foreach (CallTrace call in runtimeContext.Calls.AllCallers())
            {
                if (call.CalledToken == returnFromFunction)
                {
                    functionInStack = true;
                    break;
                }
            }
            if (!functionInStack)
            {
                runtimeContext.RaiseRuntimeError(returnFromFunction, RuntimeErrorType.ReturnToNonCaller, "function is not on the call stack");
            }
            List<LispToken> returnValues;
            if (passedArgs.Count > 1)
            {
                LispToken returnValue = passedArgs[1];
                if (returnValue.Type == LispDataType.SpecialToken && ((SpecialLispToken)returnValue).SpecialType == LispSpecialType.ReturnTuple)
                {
                    LispReturnTuple returnTuple = (LispReturnTuple)returnValue;
                    returnValues = returnTuple.ReturnValues;
                } else
                {
                    returnValues = new List<LispToken>();
                    returnValues.Add(returnValue);
                }
            } else
            {
                returnValues = new List<LispToken>();
            }
            yield return new LispImmediateReturnToken(returnFromFunction, returnValues);
        }
    }
}

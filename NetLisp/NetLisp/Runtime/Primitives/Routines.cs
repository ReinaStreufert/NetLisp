using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    abstract class RoutineGenerator : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected abstract LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, RuntimeContext runtimeContext);

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "Expected 2 arguments");
            }
            LispToken lambdaArgList = passedArgs[0];
            LispToken lambdaBody = passedArgs[1];
            if (lambdaArgList.Type != LispDataType.List)
            {
                runtimeContext.RaiseRuntimeError(lambdaArgList, RuntimeErrorType.ArgumentMismatchError, "Expected list got " + lambdaArgList.Type.ToString().ToLower());
            }
            if (lambdaBody.Type != LispDataType.List)
            {
                runtimeContext.RaiseRuntimeError(lambdaBody, RuntimeErrorType.ArgumentMismatchError, "Expected list got " + lambdaBody.Type.ToString().ToLower());
            }
            LispList castedArgList = (LispList)lambdaArgList;
            LispList castedBody = (LispList)lambdaBody;
            LispSymbol[] args = new LispSymbol[castedArgList.Items.Count];
            int argsI = 0;
            foreach (LispToken token in castedArgList.Items)
            {
                if (token.Type == LispDataType.Symbol)
                {
                    args[argsI] = (LispSymbol)token;
                    argsI++;
                } else
                {
                    runtimeContext.RaiseRuntimeError(token, RuntimeErrorType.ArgumentMismatchError, "Expected symbol got " + token.Type.ToString().ToLower());
                }
            }
            yield return ConstructRoutine(new LispExecutableBody(castedBody), args, runtimeContext);
        }
    }
    class Lambda : RoutineGenerator
    {
        protected override LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, RuntimeContext runtimeContext)
        {
            return new LispFunction(body, ScopeStack.ConstructFromScope(runtimeContext.Scopes.CurrentScope), args);
        }
    }
    class Macro : RoutineGenerator
    {
        protected override LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, RuntimeContext runtimeContext)
        {
            return new LispMacro(body, args);
        }
    }
}

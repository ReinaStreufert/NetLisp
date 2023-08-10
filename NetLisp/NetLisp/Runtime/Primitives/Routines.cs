using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    public abstract class RoutineGenerator : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected abstract LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, ArgumentDefinedMetadata metadata, RuntimeContext runtimeContext);

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count < 2 || passedArgs.Count > 3)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "Expected 2 or 3 arguments");
            }
            LispList argList = runtimeContext.Assert<LispList>(passedArgs[0], LispDataType.List);
            LispList? metadataBody;
            LispList body;
            if (passedArgs.Count > 2)
            {
                metadataBody = runtimeContext.Assert<LispList>(passedArgs[1], LispDataType.List);
                body = runtimeContext.Assert<LispList>(passedArgs[2], LispDataType.List);
            } else
            {
                metadataBody = null;
                body = runtimeContext.Assert<LispList>(passedArgs[1], LispDataType.List);
            }
            LispSymbol[] args = new LispSymbol[argList.Items.Count];
            int argsI = 0;
            foreach (LispToken token in argList.Items)
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
            ArgumentDefinedMetadata? metadata = null;
            if (metadataBody != null)
            {
                metadata = ArgumentDefinedMetadata.Parse(runtimeContext, args, metadataBody);
            }
            yield return ConstructRoutine(new LispExecutableBody(body), args, metadata, runtimeContext);
        }
    }
    class Lambda : RoutineGenerator
    {
        protected override LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, ArgumentDefinedMetadata metadata, RuntimeContext runtimeContext)
        {
            return new LispFunction(body, ScopeStack.ConstructFromScope(runtimeContext.Scopes.CurrentScope), metadata, args);
        }
    }
    class Macro : RoutineGenerator
    {
        protected override LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, ArgumentDefinedMetadata metadata, RuntimeContext runtimeContext)
        {
            return new LispMacro(body, ScopeStack.ConstructFromScope(runtimeContext.Scopes.CurrentScope), metadata, args);
        }
    }
}

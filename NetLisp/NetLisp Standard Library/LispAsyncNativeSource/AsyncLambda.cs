using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LispAsyncNativeSource
{
    class AsyncLambda : NetLisp.Runtime.Primitives.RoutineGenerator
    {
        protected override LispToken ConstructRoutine(ExecutableBody body, LispSymbol[] args, ArgumentDefinedMetadata metadata, RuntimeContext runtimeContext)
        {
            return new LispAsyncFunction(body, ScopeStack.ConstructFromScope(runtimeContext.Scopes.CurrentScope), metadata, args);
        }
    }
}

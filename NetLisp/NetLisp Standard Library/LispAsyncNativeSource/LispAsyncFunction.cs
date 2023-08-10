using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LispAsyncNativeSource
{
    public class LispAsyncFunction : LispFunction
    {
        public LispAsyncFunction(ExecutableBody functionBody, ScopeStack executingScope, ArgumentDefinedMetadata? metadata, params LispSymbol[] arguments) : base(functionBody, executingScope, metadata, arguments) { }
        // this cant be this simple, right ?
        public override IEnumerable<LispToken> Call(LispList target, RuntimeContext runtimeContext)
        {
            RuntimeContext asyncRuntimeContext = new RuntimeContext(runtimeContext.SandboxOptions);
            asyncRuntimeContext.Calls.CurrentlyEvaluatingToken = this;
            Task<LispToken[]> task = Task.Run<LispToken[]>(() =>
            {
                return base.Call(target, asyncRuntimeContext).ToArray();
            });
            yield return new LispPromise(task);
        }

        public LispPromise CallAfter(Task task, LispList target, RuntimeContext runtimeContext)
        {
            RuntimeContext asyncRuntimeContext = new RuntimeContext(runtimeContext.SandboxOptions);
            asyncRuntimeContext.Calls.CurrentlyEvaluatingToken = this;
            Task<LispToken[]> callTask = task.ContinueWith<LispToken[]>((Task t) =>
            {
                return base.Call(target, asyncRuntimeContext).ToArray();
            });
            return new LispPromise(callTask);
        }

        public LispPromise ChainAfter(Task<LispToken[]> task, LispList target, RuntimeContext runtimeContext)
        {
            RuntimeContext asyncRuntimeContext = new RuntimeContext(runtimeContext.SandboxOptions);
            asyncRuntimeContext.Calls.CurrentlyEvaluatingToken = this;
            Task<LispToken[]> callTask = task.ContinueWith<LispToken[]>((Task t) =>
            {
                if (t.IsFaulted)
                {
                    throw t.Exception.InnerException;
                } else
                {
                    target.Items.Insert(1, new LispPromise(task));
                    return base.Call(target, asyncRuntimeContext).ToArray();
                }
            });
            return new LispPromise(callTask);
        }
    }
}

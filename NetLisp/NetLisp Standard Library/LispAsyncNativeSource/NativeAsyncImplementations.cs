using LispDNBridgeNativeSource;
using LispTableNativeSource;
using NetLisp.Data;
using NetLisp.Runtime;
using System.Threading.Tasks;

namespace LispAsyncNativeSource
{
    public class NativeAsyncImplementations : INativeSource
    {
        public LispToken OnSourceLoad(RuntimeContext runtimeContext)
        {
            Scope global = runtimeContext.Scopes.GlobalScope;
            LispTable asyncTable = (LispTable)runtimeContext.LoadModule(new LispSymbol("async")).LoadResult;

            asyncTable[new LispSymbol("alambda")] = new AsyncLambda();
            asyncTable[new LispSymbol("finished?")] = new LispFunction(new NativeExecutableBody(finished), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promise"));
            asyncTable[new LispSymbol("failed?")] = new LispFunction(new NativeExecutableBody(failed), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promise"));
            asyncTable[new LispSymbol("result")] = new LispFunction(new NativeExecutableBody(result), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promise"));
            asyncTable[new LispSymbol("await")] = new LispFunction(new NativeExecutableBody(await), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promise"));
            asyncTable[new LispSymbol("awaitlist")] = new LispFunction(new NativeExecutableBody(awaitlist), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promiselist"));
            asyncTable[new LispSymbol("callafter")] = new LispFunction(new NativeExecutableBody(callafter), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promise"), new LispSymbol("afunc"), new LispSymbol("args"));
            asyncTable[new LispSymbol("chainafter")] = new LispFunction(new NativeExecutableBody(chainafter), ScopeStack.ConstructFromScope(global), null, new LispSymbol("promise"), new LispSymbol("afunc"), new LispSymbol("args"));
            asyncTable[new LispSymbol("callaftert")] = new LispFunction(new NativeExecutableBody(callaftert), ScopeStack.ConstructFromScope(global), null, new LispSymbol("task"), new LispSymbol("afunc"), new LispSymbol("args"));

            return asyncTable;
        }

        public IEnumerable<LispToken> finished(RuntimeContext runtimeContext)
        {
            LispPromise promise = runtimeContext.Assert<LispPromise>(runtimeContext.Scopes.CurrentScope.Get("promise"), LispPromise.PromiseExtendedTypeInfo);
            yield return new LispBoolean(promise.Task.IsCompleted);
        }

        public IEnumerable<LispToken> failed(RuntimeContext runtimeContext)
        {
            LispPromise promise = runtimeContext.Assert<LispPromise>(runtimeContext.Scopes.CurrentScope.Get("promise"), LispPromise.PromiseExtendedTypeInfo);
            yield return new LispBoolean(promise.Task.IsFaulted);
        }

        public IEnumerable<LispToken> result(RuntimeContext runtimeContext)
        {
            LispPromise promise = runtimeContext.Assert<LispPromise>(runtimeContext.Scopes.CurrentScope.Get("promise"), LispPromise.PromiseExtendedTypeInfo);
            Task<LispToken[]> task = promise.Task;
            if (task.IsFaulted)
            {
                Exception ex = task.Exception.InnerException;
                if (ex.GetType() == typeof(LispRuntimeException))
                {
                    LispRuntimeException lispEx = (LispRuntimeException)ex;
                    throw lispEx;
                } else
                {
                    runtimeContext.RaiseRuntimeError(promise, RuntimeErrorType.DotNetException, ex.ToString());
                }
            } else
            {
                foreach (LispToken token in task.Result)
                {
                    yield return token;
                }
            }
        }

        public IEnumerable<LispToken> await(RuntimeContext runtimeContext)
        {
            LispPromise promise = runtimeContext.Assert<LispPromise>(runtimeContext.Scopes.CurrentScope.Get("promise"), LispPromise.PromiseExtendedTypeInfo);
            try
            {
                promise.Task.Wait();
            } catch (AggregateException ex)
            {
                if (ex.InnerException.GetType() == typeof(LispRuntimeException))
                {
                    LispRuntimeException lispEx = (LispRuntimeException)(ex.InnerException);
                    throw lispEx;
                }
                else
                {
                    runtimeContext.RaiseRuntimeError(promise, RuntimeErrorType.DotNetException, ex.InnerException.ToString());
                }
            }
            foreach (LispToken token in promise.Task.Result)
            {
                yield return token;
            }
        }

        public IEnumerable<LispToken> awaitlist(RuntimeContext runtimeContext)
        {
            LispList promiseList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("promiselist"), LispDataType.List);
            Task[] waitTasks = new Task[promiseList.Items.Count];
            for (int i = 0; i < promiseList.Items.Count; i++)
            {
                LispPromise promise = runtimeContext.Assert<LispPromise>(promiseList.Items[i], LispPromise.PromiseExtendedTypeInfo);
                waitTasks[i] = promise.Task;
            }
            try
            {
                Task.WaitAll(waitTasks);
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException.GetType() == typeof(LispRuntimeException))
                {
                    LispRuntimeException lispEx = (LispRuntimeException)(ex.InnerException);
                    throw lispEx;
                }
                else
                {
                    runtimeContext.RaiseRuntimeError(promiseList, RuntimeErrorType.DotNetException, ex.InnerException.ToString());
                }
            }
            yield break;
        }

        public IEnumerable<LispToken> callafter(RuntimeContext runtimeContext)
        {
            LispPromise promise = runtimeContext.Assert<LispPromise>(runtimeContext.Scopes.CurrentScope.Get("promise"), LispPromise.PromiseExtendedTypeInfo);
            LispToken afuncToken = runtimeContext.Scopes.CurrentScope.Get("afunc");
            LispList argList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("args"), LispDataType.List);

            LispList target = new LispList(argList.Items.Prepend(afuncToken).ToList());

            if (afuncToken.GetType() != typeof(LispAsyncFunction))
            {
                runtimeContext.RaiseRuntimeError(afuncToken, RuntimeErrorType.ArgumentMismatchError, "Expected async function");
            }
            LispAsyncFunction afunc = (LispAsyncFunction)afuncToken;

            yield return afunc.CallAfter(promise.Task, target, runtimeContext);
        }

        public IEnumerable<LispToken> chainafter(RuntimeContext runtimeContext)
        {
            LispPromise promise = runtimeContext.Assert<LispPromise>(runtimeContext.Scopes.CurrentScope.Get("promise"), LispPromise.PromiseExtendedTypeInfo);
            LispToken afuncToken = runtimeContext.Scopes.CurrentScope.Get("afunc");
            LispList argList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("args"), LispDataType.List);

            LispList target = new LispList(argList.Items.Prepend(afuncToken).ToList());

            if (afuncToken.GetType() != typeof(LispAsyncFunction))
            {
                runtimeContext.RaiseRuntimeError(afuncToken, RuntimeErrorType.ArgumentMismatchError, "Expected async function");
            }
            LispAsyncFunction afunc = (LispAsyncFunction)afuncToken;

            yield return afunc.ChainAfter(promise.Task, target, runtimeContext);
        }

        public IEnumerable<LispToken> callaftert(RuntimeContext runtimeContext)
        {
            DotnetInstance taskInstance = runtimeContext.Assert<DotnetInstance>(runtimeContext.Scopes.CurrentScope.Get("task"), DotnetInstance.DotnetInstanceExtendedTypeInfo);
            if (!taskInstance.Instance.GetType().IsAssignableTo(typeof(Task)))
            {
                runtimeContext.RaiseRuntimeError(taskInstance, RuntimeErrorType.ArgumentMismatchError, "Expected dotnet Task object");
            }
            Task task = (Task)taskInstance.Instance;
            LispToken afuncToken = runtimeContext.Scopes.CurrentScope.Get("afunc");
            LispList argList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("args"), LispDataType.List);

            LispList target = new LispList(argList.Items.Prepend(afuncToken).ToList());

            if (afuncToken.GetType() != typeof(LispAsyncFunction))
            {
                runtimeContext.RaiseRuntimeError(afuncToken, RuntimeErrorType.ArgumentMismatchError, "Expected async function");
            }
            LispAsyncFunction afunc = (LispAsyncFunction)afuncToken;

            yield return afunc.CallAfter(task, target, runtimeContext);
        }
    }
}
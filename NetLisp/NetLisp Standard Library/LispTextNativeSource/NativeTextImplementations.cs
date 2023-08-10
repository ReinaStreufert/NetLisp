using NetLisp.Runtime;
using NetLisp.Data;

namespace LispTextNativeSource
{
    public class NativeTextImplementations : INativeSource
    {
        public LispToken OnSourceLoad(RuntimeContext runtimeContext)
        {
            Scope global = runtimeContext.Scopes.GlobalScope;
            global.Define("txlen", new LispFunction(new NativeExecutableBody(TxLen), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("str")
            ));
            global.Define("txconc", new LispFunction(new NativeExecutableBody(TxConc), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("str1"),
                new LispSymbol("str2")
            ));
            global.Define("txrang", new LispFunction(new NativeExecutableBody(TxRang), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("str"),
                new LispSymbol("rStart"),
                new LispSymbol("rLen")
            ));

            return new LispList();
        }

        public IEnumerable<LispToken> TxLen(RuntimeContext runtimeContext)
        {
            LispString str = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("str"), LispDataType.String);
            yield return new LispNumber(str.Value.Length);
        }

        public IEnumerable<LispToken> TxConc(RuntimeContext runtimeContext)
        {
            LispString str1 = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("str1"), LispDataType.String);
            LispString str2 = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("str2"), LispDataType.String);
            yield return new LispString(str1.Value + str2.Value);
        }

        public IEnumerable<LispToken> TxRang(RuntimeContext runtimeContext)
        {
            LispString str = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("str"), LispDataType.String);
            LispNumber rStart = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("rStart"), LispDataType.Number);
            LispNumber rLen = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("rLen"), LispDataType.Number);
            if (rStart.Value < 0 || rStart.Value + rLen.Value - 1 >= str.Value.Length)
            {
                runtimeContext.RaiseRuntimeError(rLen, RuntimeErrorType.Other, "Substring out of range");
            }
            yield return new LispString(str.Value.Substring((int)Math.Round(rStart.Value), (int)Math.Round(rLen.Value)));
        }
    }
}
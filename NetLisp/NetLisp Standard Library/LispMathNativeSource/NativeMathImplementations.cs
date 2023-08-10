using NetLisp.Data;
using NetLisp.Runtime;
using LispTableNativeSource;

namespace LispMathNativeSource
{
    public class NativeMathImplementations : INativeSource
    {
        private Random r = new Random();
        // incomplete
        public LispToken OnSourceLoad(RuntimeContext runtimeContext)
        {
            Scope global = runtimeContext.Scopes.GlobalScope;
            LispTable mathTable = (LispTable)runtimeContext.LoadModule(new LispSymbol("math")).LoadResult;

            // constants
            mathTable[new LispSymbol("pi")] = new LispNumber(Math.PI);
            mathTable[new LispSymbol("e")] = new LispNumber(Math.E);

            mathTable[new LispSymbol("floor")] = new LispFunction(new NativeExecutableBody(Floor), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("num")
            );
            mathTable[new LispSymbol("ceil")] = new LispFunction(new NativeExecutableBody(Ceil), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("num")
            );
            mathTable[new LispSymbol("round")] = new LispFunction(new NativeExecutableBody(Round), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("num")
            );
            mathTable[new LispSymbol("rand")] = new LispFunction(new NativeExecutableBody(Rand), ScopeStack.ConstructFromScope(global), null);

            mathTable[new LispSymbol("^")] = new LispFunction(new NativeExecutableBody(Exp), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("num"),
                new LispSymbol("exp")
            );

            return mathTable;
        }

        public IEnumerable<LispToken> Floor(RuntimeContext runtimeContext)
        {
            LispNumber num = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("num"), LispDataType.Number);
            yield return new LispNumber(Math.Floor(num.Value));
        }

        public IEnumerable<LispToken> Ceil(RuntimeContext runtimeContext)
        {
            LispNumber num = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("num"), LispDataType.Number);
            yield return new LispNumber(Math.Ceiling(num.Value));
        }

        public IEnumerable<LispToken> Round(RuntimeContext runtimeContext)
        {
            LispNumber num = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("num"), LispDataType.Number);
            yield return new LispNumber(Math.Round(num.Value));
        }

        public IEnumerable<LispToken> Rand(RuntimeContext runtimeContext)
        {
            yield return new LispNumber(r.NextDouble());
        }

        public IEnumerable<LispToken> Exp(RuntimeContext runtimeContext)
        {
            LispNumber num = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("num"), LispDataType.Number);
            LispNumber exp = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("exp"), LispDataType.Number);
            yield return new LispNumber(Math.Pow(num.Value, exp.Value));
        }
    }
}
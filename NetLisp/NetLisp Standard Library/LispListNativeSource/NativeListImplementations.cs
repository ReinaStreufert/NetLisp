using NetLisp;
using NetLisp.Runtime;
using NetLisp.Data;

namespace LispListNativeSource
{
    public class NativeListImplementations : INativeSource
    {
        public LispToken OnSourceLoad(RuntimeContext runtimeContext)
        {
            LispSymbol[] oneArgListFunc = new[]
            {
                new LispSymbol("tList")
            };
            LispSymbol[] listItemListFunc = new[]
            {
                new LispSymbol("tList"),
                new LispSymbol("item")
            };
            LispSymbol[] listNumListFunc = new[]
            {
                new LispSymbol("tList"),
                new LispSymbol("n")
            };
            LispSymbol[] listNumItemListFunc = new[]
            {
                new LispSymbol("tList"),
                new LispSymbol("n"),
                new LispSymbol("item")
            };

            Scope global = runtimeContext.Scopes.GlobalScope;
            global.Define("lsnew", new LsNew());
            global.Define("lslen", new LispFunction(new NativeExecutableBody(LsLen), ScopeStack.ConstructFromScope(global), null, oneArgListFunc));
            global.Define("lspush", new LispFunction(new NativeExecutableBody(LsPush), ScopeStack.ConstructFromScope(global), null, listItemListFunc));
            global.Define("lspop", new LispFunction(new NativeExecutableBody(LsPop), ScopeStack.ConstructFromScope(global), null, oneArgListFunc));
            global.Define("lsprep", new LispFunction(new NativeExecutableBody(LsPrep), ScopeStack.ConstructFromScope(global), null, listItemListFunc));
            global.Define("lstake", new LispFunction(new NativeExecutableBody(LsTake), ScopeStack.ConstructFromScope(global), null, oneArgListFunc));
            global.Define("lsn", new LispFunction(new NativeExecutableBody(LsN), ScopeStack.ConstructFromScope(global), null, listNumListFunc));
            global.Define("lsinsn", new LispFunction(new NativeExecutableBody(LsInsN), ScopeStack.ConstructFromScope(global), null, listNumItemListFunc));
            global.Define("lsdeln", new LispFunction(new NativeExecutableBody(LsDelN), ScopeStack.ConstructFromScope(global), null, listNumListFunc));

            return new LispList();
        }

        public class LsNew : LispSpecialForm
        {
            public override bool EvaluateArguments => true;

            protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
            {
                yield return new LispList() { Items = passedArgs };
            }
        }

        public IEnumerable<LispToken> LsLen(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            yield return new LispNumber(tList.Items.Count);
        }

        public IEnumerable<LispToken> LsPush(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            LispToken item = runtimeContext.Scopes.CurrentScope.Get("item");
            List<LispToken> newListItems = new List<LispToken>(tList.Items);
            newListItems.Add(item);
            yield return new LispList(newListItems);
        }

        public IEnumerable<LispToken> LsPop(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            List<LispToken> newListItems = tList.Items.GetRange(0, tList.Items.Count - 1);
            yield return new LispList(newListItems);
        }

        public IEnumerable<LispToken> LsPrep(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            LispToken item = runtimeContext.Scopes.CurrentScope.Get("item");
            List<LispToken> newListItems = new List<LispToken>(tList.Items);
            newListItems.Insert(0, item);
            yield return new LispList(newListItems);
        }

        public IEnumerable<LispToken> LsTake(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            List<LispToken> newListItems = tList.Items.GetRange(1, tList.Items.Count - 1);
            yield return new LispList(newListItems);
        }

        public IEnumerable<LispToken> LsN(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            LispNumber n = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("n"), LispDataType.Number);
            if (double.IsNaN(n.Value))
            {
                runtimeContext.RaiseRuntimeError(n, RuntimeErrorType.Other, "Index is NaN");
            }
            int ind = (int)Math.Round(n.Value);
            if (ind < 0 || ind >= tList.Items.Count)
            {
                runtimeContext.RaiseRuntimeError(n, RuntimeErrorType.Other, "Index out of range");
            }
            yield return tList.Items[ind];
        }

        public IEnumerable<LispToken> LsInsN(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            LispNumber n = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("n"), LispDataType.Number);
            if (double.IsNaN(n.Value))
            {
                runtimeContext.RaiseRuntimeError(n, RuntimeErrorType.Other, "Index is NaN");
            }
            LispToken item = runtimeContext.Scopes.CurrentScope.Get("item");
            int ind = (int)Math.Round(n.Value);
            if (ind < 0 || ind > tList.Items.Count)
            {
                runtimeContext.RaiseRuntimeError(n, RuntimeErrorType.Other, "Index out of range");
            }
            List<LispToken> newListItems = new List<LispToken>(tList.Items);
            newListItems.Insert(ind, item);
            yield return new LispList(newListItems);
        }

        public IEnumerable<LispToken> LsDelN(RuntimeContext runtimeContext)
        {
            LispList tList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("tList"), LispDataType.List);
            LispNumber n = runtimeContext.Assert<LispNumber>(runtimeContext.Scopes.CurrentScope.Get("n"), LispDataType.Number);
            if (double.IsNaN(n.Value))
            {
                runtimeContext.RaiseRuntimeError(n, RuntimeErrorType.Other, "Index is NaN");
            }
            int ind = (int)Math.Round(n.Value);
            if (ind < 0 || ind >= tList.Items.Count)
            {
                runtimeContext.RaiseRuntimeError(n, RuntimeErrorType.Other, "Index out of range");
            }
            List<LispToken> newListItems = new List<LispToken>(tList.Items);
            newListItems.RemoveAt(ind);
            yield return new LispList(newListItems);
        }
    }
}
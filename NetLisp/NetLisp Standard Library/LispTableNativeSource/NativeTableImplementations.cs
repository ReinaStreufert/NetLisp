using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LispTableNativeSource
{
    public class NativeTableImplementations : INativeSource
    {
        public LispToken OnSourceLoad(RuntimeContext runtimeContext)
        {
            Scope global = runtimeContext.Scopes.GlobalScope;
            global.Define("tbnew", new LispFunction(new NativeExecutableBody(TbNew), ScopeStack.ConstructFromScope(global), null));
            global.Define("tbhaskey", new LispFunction(new NativeExecutableBody(TbHasKey), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("table"),
                new LispSymbol("key")
            ));
            global.Define("tbgval", new LispFunction(new NativeExecutableBody(TbGVal), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("table"),
                new LispSymbol("key")
            ));
            global.Define("tbsval", new LispFunction(new NativeExecutableBody(TbSVal), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("table"),
                new LispSymbol("key"),
                new LispSymbol("value")
            ));
            global.Define("tbdelkey", new LispFunction(new NativeExecutableBody(TbDelKey), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("table"),
                new LispSymbol("key")
            ));
            global.Define("tbpairs", new LispFunction(new NativeExecutableBody(TbPairs), ScopeStack.ConstructFromScope(global), null,
                new LispSymbol("table")
            ));

            global.Define("tbtypestr", new LispString(LispTypeInfo.FromExtendedTypeInfo(LispTable.TableExtendedTypeInfo).TypeStr));

            return new LispList();
        }

        public IEnumerable<LispToken> TbNew(RuntimeContext runtimeContext)
        {
            yield return new LispTable();
        }

        public IEnumerable<LispToken> TbHasKey(RuntimeContext runtimeContext)
        {
            LispTable table = runtimeContext.Assert<LispTable>(runtimeContext.Scopes.CurrentScope.Get("table"), LispTable.TableExtendedTypeInfo);
            LispToken key = runtimeContext.Scopes.CurrentScope.Get("key");
            yield return new LispBoolean(table.ContainsKey(key));
        }

        public IEnumerable<LispToken> TbGVal(RuntimeContext runtimeContext)
        {
            LispTable table = runtimeContext.Assert<LispTable>(runtimeContext.Scopes.CurrentScope.Get("table"), LispTable.TableExtendedTypeInfo);
            LispToken key = runtimeContext.Scopes.CurrentScope.Get("key");

            LispToken? val = table[key];
            if (val == null)
            {
                runtimeContext.RaiseRuntimeError(key, RuntimeErrorType.Other, "No such key exists in the table");
            }
            yield return val;
        }

        public IEnumerable<LispToken> TbSVal(RuntimeContext runtimeContext)
        {
            LispTable table = runtimeContext.Assert<LispTable>(runtimeContext.Scopes.CurrentScope.Get("table"), LispTable.TableExtendedTypeInfo);
            LispToken key = runtimeContext.Scopes.CurrentScope.Get("key");
            LispToken value = runtimeContext.Scopes.CurrentScope.Get("value");

            table[key] = value;
            yield break;
        }

        public IEnumerable<LispToken> TbDelKey(RuntimeContext runtimeContext)
        {
            LispTable table = runtimeContext.Assert<LispTable>(runtimeContext.Scopes.CurrentScope.Get("table"), LispTable.TableExtendedTypeInfo);
            LispToken key = runtimeContext.Scopes.CurrentScope.Get("key");

            if (!table.DeleteKey(key))
            {
                runtimeContext.RaiseRuntimeError(key, RuntimeErrorType.Other, "No such key exists in the table");
            }
            yield break;
        }

        public IEnumerable<LispToken> TbPairs(RuntimeContext runtimeContext)
        {
            LispTable table = runtimeContext.Assert<LispTable>(runtimeContext.Scopes.CurrentScope.Get("table"), LispTable.TableExtendedTypeInfo);

            LispList result = new LispList();
            foreach (KeyValuePair<LispToken, LispToken> pair in table)
            {
                LispList pairList = new LispList();
                pairList.Items.Add(pair.Key);
                pairList.Items.Add(pair.Value);
                result.Items.Add(pairList);
            }
            yield return result;
        }
    }
}

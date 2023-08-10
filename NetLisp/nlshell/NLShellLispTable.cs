using LispTableNativeSource;
using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlshell
{
    class NLShellLispTable : LispTable
    {
        public NLShellContext ShellContext { get; set; }
        public NLShellLispTable(NLShellContext shellContext)
        {
            ShellContext = shellContext;
            Scope global = shellContext.RuntimeContext.Scopes.GlobalScope;
            this[new LispSymbol("shver")] = new LispFunction(new NativeExecutableBody(shver), ScopeStack.ConstructFromScope(global), null);
            this[new LispSymbol("nlver")] = new LispFunction(new NativeExecutableBody(nlver), ScopeStack.ConstructFromScope(global), null);
            this[new LispSymbol("nlctx")] = new LispFunction(new NativeExecutableBody(nlctx), ScopeStack.ConstructFromScope(global), null);
        }
        private IEnumerable<LispToken> shver(RuntimeContext runtimeContext)
        {
            yield return new LispString(NLShellContext.Version);
        }
        private IEnumerable<LispToken> nlver(RuntimeContext runtimeContext)
        {
            yield return new LispString(RuntimeContext.Version);
        }
        private IEnumerable<LispToken> nlctx(RuntimeContext runtimeContext)
        {
            yield break;
        }
    }
}

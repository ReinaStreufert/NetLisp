using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispMacro : ArgumentDefinedLispRoutine
    {
        public override LispDataType Type => LispDataType.Macro;

        public LispMacro(ExecutableBody functionBody, params LispSymbol[] arguments)
        {
            Body = functionBody;
            Arguments = arguments.ToList();
        }

        protected override ScopeStack GetExecutingScope(RuntimeContext runtimeContext)
        {
            return runtimeContext.Scopes;
        }

        protected override ExecutableBody PreprocessBody(RuntimeContext runtimeContext)
        {
            if (Body.Type == ExecutableBodyType.LispDefinedBody)
            {
                LispExecutableBody inputBody = (LispExecutableBody)Body;
                return new LispExecutableBody(preprocessListRecursive(inputBody.Expression, runtimeContext));
            } else
            {
                return Body;
            }
        }
        private LispList preprocessListRecursive(LispList list, RuntimeContext runtimeContext)
        {
            List<LispToken> newList = new List<LispToken>();
            foreach (LispToken token in list.Items)
            {
                if (token.Type == LispDataType.Symbol)
                {
                    LispSymbol symbol = (LispSymbol)token;
                    bool symbolIsArgument = false;
                    foreach (LispSymbol argument in Arguments)
                    {
                        if (argument.Value == symbol.Value)
                        {
                            LispToken evaluatedSymbol = argument.Evaluate(runtimeContext).First();
                            if (symbol.Quoted)
                            {
                                evaluatedSymbol.Quoted = true;
                            }
                            newList.Add(evaluatedSymbol);
                            symbolIsArgument = true;
                            break;
                        }
                    }
                    if (!symbolIsArgument)
                    {
                        newList.Add(symbol);
                    }
                } else if (token.Type == LispDataType.List)
                {
                    newList.Add(preprocessListRecursive((LispList)token, runtimeContext));
                } else
                {
                    newList.Add(token);
                }
            }
            LispList result = new LispList() { Items = newList, SourceLocation = list.SourceLocation };
            if (list.Quoted)
            {
                result.Quoted = true;
            }
            return result;
        }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }

        public override string ToString()
        {
            return Text.LispTokenWriter.MacroToString;
        }
    }
}

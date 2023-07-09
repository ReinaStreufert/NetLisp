using NetLisp.Data;
using NetLisp.Runtime;
using NetLisp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispSymbol : LispToken
    {
        public override LispDataType Type => LispDataType.Symbol;
        public override bool TypeRequiresEvaluation => true;
        public override bool TypeCanBeExecuted => false;

        public LispSymbol(string val)
        {
            Value = val;
        }

        private string value;
        public string Value
        {
            get
            {
                return value;
            }
            set
            {
                if (RegularExpressions.EnsureEntireMatch(RegularExpressions.SymbolMatchExpression, value))
                {
                    this.value = value;
                } else
                {
                    throw new FormatException("Symbol does not conform to value rules");
                }
            }
        }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            LispToken? defValue = runtimeContext.Scopes.CurrentScope.Get(value);
            if (defValue == null)
            {
                runtimeContext.RaiseRuntimeError(/*...*/);
            } else
            {
                yield return defValue;
            }
        }

        public override string ToString()
        {
            return Value;
        }
    }
}

using NetLisp.Runtime;
using NetLisp.Runtime.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispString : LispToken
    {
        public override LispDataType Type => LispDataType.String;
        public override bool TypeCanBeExecuted => false;

        public LispString(string val)
        {
            Value = val;
        }

        public string Value { get; set; }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }

        public override bool CompareValue(LispToken token)
        {
            return (token.Type == LispDataType.String && ((LispString)token).Value == Value);
        }

        public override int HashValue()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return "\"" + Value + "\"";
        }
    }
}

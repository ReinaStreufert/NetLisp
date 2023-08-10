using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispNumber : LispToken
    {
        public override LispDataType Type => LispDataType.Number;
        public override bool TypeCanBeExecuted => false;

        public LispNumber(double val)
        {
            this.Value = val;
        }

        public double Value { get; set; } = 0F;

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return new LispNumber(Value) { SourceLocation = SourceLocation };
        }

        public override bool CompareValue(LispToken token)
        {
            return (token.Type == LispDataType.Number && ((LispNumber)token).Value == Value);
        }

        public override int HashValue()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}

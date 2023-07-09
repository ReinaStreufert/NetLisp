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
        public override bool TypeRequiresEvaluation => false;
        public override bool TypeCanBeExecuted => false;

        public LispNumber(float val)
        {
            this.Value = val;
        }

        public float Value { get; set; } = 0F;

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}

using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public class LispBoolean : LispToken
    {
        public override LispDataType Type => LispDataType.Boolean;
        public override bool TypeRequiresEvaluation => false;
        public override bool TypeCanBeExecuted => false;

        public LispBoolean(bool val)
        {
            Value = val;
        }

        public bool Value { get; set; }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }

        public override string ToString()
        {
            return Value.ToString().ToLower();
        }
    }
}

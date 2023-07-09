using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    public abstract class LispSpecialForm : ExecutableLispToken
    {
        public sealed override LispDataType Type => LispDataType.SpecialForm;

        protected abstract IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext);

        public sealed override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }
        protected override IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext)
        {
            List<LispToken> passedArgs = target.Items.Skip(1).ToList();
            return InnerExecute(passedArgs, runtimeContext);
        }

        public sealed override string ToString()
        {
            return Text.LispTokenWriter.SpecialFormToString;
        }
    }
}

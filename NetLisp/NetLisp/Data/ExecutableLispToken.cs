using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetLisp.Data;
using NetLisp.Runtime;

namespace NetLisp.Data
{
    public abstract class ExecutableLispToken : LispToken
    {
        public override bool TypeRequiresEvaluation => false;
        public override bool TypeCanBeExecuted => true;

        protected abstract IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext);

        public IEnumerable<LispToken> Call(LispList target, RuntimeContext runtimeContext)
        {
            runtimeContext.Calls.Push(this);
            IEnumerable<LispToken> returnValues = Execute(target, runtimeContext);
            runtimeContext.Calls.Pop();
            return returnValues;
        }
    }
    public abstract class ArgumentDefinedLispRoutine : ExecutableLispToken
    {
        public List<LispSymbol> Arguments { get; set; }
        public ExecutableBody Body { get; set; }

        protected override IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext)
        {
            // first item in target is always this token. following items are argument values.
            int passedArgumentCount = target.Items.Count - 1;
            if (passedArgumentCount != Arguments.Count)
            {
                runtimeContext.RaiseRuntimeError(/*...*/);
            }
            // push scope and define arguments
            runtimeContext.Scopes.Push();
            Scope routineScope = runtimeContext.Scopes.CurrentScope;
            for (int i = 0; i < Arguments.Count; i++)
            {
                string argumentName = Arguments[i].Value;
                LispToken argumentValue = target.Items[i + 1];
                routineScope.Define(argumentName, argumentValue);
            }
            // execute function
            IEnumerable<LispToken> returnValues = Body.Execute(runtimeContext);
            // pop scope and return
            runtimeContext.Scopes.Pop();
            foreach (LispToken returnValue in returnValues)
            {
                yield return returnValue;
            }
        }
    }
}

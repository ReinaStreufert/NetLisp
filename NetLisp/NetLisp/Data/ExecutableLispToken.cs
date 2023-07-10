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
        public override bool TypeCanBeExecuted => true;

        protected abstract IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext);

        public IEnumerable<LispToken> Call(LispList target, RuntimeContext runtimeContext)
        {
            runtimeContext.Calls.Push(this);
            IEnumerable<LispToken> returnValues = Execute(target, runtimeContext);
            foreach (LispToken token in returnValues)
            {
                token.SourceLocation = target.SourceLocation;
                yield return token;
            }
            runtimeContext.Calls.Pop();
        }
    }
    public abstract class ArgumentDefinedLispRoutine : ExecutableLispToken
    {
        public List<LispSymbol> Arguments { get; set; }
        public ExecutableBody Body { get; set; }

        protected abstract ScopeStack GetExecutingScope(RuntimeContext runtimeContext);

        protected override IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext)
        {
            // first item in target is always this token. following items are argument values.
            int passedArgumentCount = target.Items.Count - 1;
            if (passedArgumentCount != Arguments.Count)
            {
                // try to get a name for this routine for the error
                string? routName = runtimeContext.Scopes.CurrentScope.Search(this);
                string routType = Type.ToString();
                if (routName == null)
                {
                    routName = "<anonymous>";
                }
                runtimeContext.RaiseRuntimeError(target, RuntimeErrorType.ArgumentMismatchError, routType + " '" + routName + "' takes " + Arguments.Count + " arguments");
            }
            // get the executing scope for this routine and replace the runtime context scope with it
            ScopeStack callerScopeStack = runtimeContext.Scopes; // keep old scope stack to be restored
            runtimeContext.Scopes = GetExecutingScope(runtimeContext);
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
            IEnumerable<LispToken> returnValues = PreprocessBody(runtimeContext).Execute(runtimeContext);
            foreach (LispToken returnValue in returnValues)
            {
                yield return returnValue;
            }
            // pop replaced scope back to start, restore scope and return
            runtimeContext.Scopes.Pop();
            runtimeContext.Scopes = callerScopeStack;
        }

        protected virtual ExecutableBody PreprocessBody(RuntimeContext runtimeContext)
        {
            // this is so macros may override this and pre-evaluate all macro argument symbols since some of them
            // wouldnt be changed to their call value during normal evaluation (like in special form calls)
            return Body;
        }
    }
}

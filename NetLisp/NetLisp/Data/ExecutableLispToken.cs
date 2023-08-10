using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetLisp.Data;
using NetLisp.Data.SpecialTokens;
using NetLisp.Runtime;

namespace NetLisp.Data
{
    public abstract class ExecutableLispToken : LispToken
    {
        public override bool TypeCanBeExecuted => true;
        public virtual ExecutableTokenMetadata Metadata { get => ExecutableTokenMetadata.Blank; }

        protected abstract IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext);

        public virtual IEnumerable<LispToken> Call(LispList target, RuntimeContext runtimeContext)
        {
            
            runtimeContext.Calls.Push(this);
            LispImmediateReturnToken immediateReturn = null;
            List<LispToken> returnValues = checkImmediateReturn(Execute(target, runtimeContext), out immediateReturn);
            while (runtimeContext.Calls.IsLoopFlagSet && immediateReturn == null)
            {
                LispList loopTarget = runtimeContext.Calls.LoopCallTarget;
                runtimeContext.Calls.ClearLoopFlag();
                returnValues = checkImmediateReturn(Execute(loopTarget, runtimeContext), out immediateReturn);
            }
            if (immediateReturn != null)
            {
                if (immediateReturn.ReturnFrom == this)
                {
                    returnValues = immediateReturn.ReturnValues;
                } else
                {
                    runtimeContext.Calls.Pop();
                    yield return immediateReturn;
                    yield break;
                }
            }
            foreach (LispToken token in returnValues)
            {
                token.SourceLocation = target.SourceLocation;
                yield return token;
            }
            
            runtimeContext.Calls.Pop();
        }
        private List<LispToken> checkImmediateReturn(IEnumerable<LispToken> execution, out LispImmediateReturnToken immediateReturn)
        {
            List<LispToken> result = new List<LispToken>();
            foreach (LispToken token in execution)
            {
                result.Add(token);
                if (token.IsImmediateReturnToken)
                {
                    immediateReturn = (LispImmediateReturnToken)token;
                    return result;
                }
            }
            immediateReturn = null;
            return result;
        }

        public override bool CompareValue(LispToken token)
        {
            return this == token; // reference compare
        }

        public override int HashValue()
        {
            return this.GetHashCode(); // reference compare
        }
    }
    public abstract class ArgumentDefinedLispRoutine : ExecutableLispToken
    {
        public LispSymbol[] Arguments { get; set; }
        public ArgumentDefinedMetadata InstanceMetadata { get; set; }
        public ExecutableBody Body { get; set; }

        public int ArgumentCount
        {
            get
            {
                return Arguments.Length;
            }
        }

        protected abstract ScopeStack GetExecutingScope(RuntimeContext runtimeContext);

        public override ExecutableTokenMetadata Metadata => InstanceMetadata;

        protected override IEnumerable<LispToken> Execute(LispList target, RuntimeContext runtimeContext)
        {
            // first item in target is always this token. following items are argument values.
            int passedArgumentCount = target.Items.Count - 1;
            if (passedArgumentCount != Arguments.Length)
            {
                // try to get a name for this routine for the error
                string? routName = runtimeContext.Scopes.CurrentScope.Search(this);
                string routType = Type.ToString();
                if (routName == null)
                {
                    routName = "<anonymous>";
                }
                runtimeContext.RaiseRuntimeError(target, RuntimeErrorType.ArgumentMismatchError, routType + " '" + routName + "' takes " + Arguments.Length + " arguments");
            }
            // get the executing scope for this routine and replace the runtime context scope with it
            ScopeStack callerScopeStack = runtimeContext.Scopes; // keep old scope stack to be restored
            runtimeContext.Scopes = GetExecutingScope(runtimeContext);
            // push scope and define arguments
            runtimeContext.Scopes.Push();
            Scope routineScope = runtimeContext.Scopes.CurrentScope;
            for (int i = 0; i < Arguments.Length; i++)
            {
                string argumentName = Arguments[i].Value;
                LispToken argumentValue = target.Items[i + 1];
                routineScope.Define(argumentName, argumentValue);
            }
            // execute function
            IEnumerable<LispToken> returnValues = PreprocessBody(runtimeContext).Execute(runtimeContext);
            foreach (LispToken returnValue in returnValues)
            {
                if (returnValue.IsImmediateReturnToken)
                {
                    // iterator will stop running in the caller so the scope-popping at the very end will never be reached
                    runtimeContext.Scopes.Pop();
                    runtimeContext.Scopes = callerScopeStack;
                }
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

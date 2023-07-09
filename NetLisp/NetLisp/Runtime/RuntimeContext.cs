using NetLisp.Data;

namespace NetLisp.Runtime
{
    public class RuntimeContext
    {
        public ScopeStack Scopes { get; private set; } = new ScopeStack();
        public CallStack Calls { get; private set; } = new CallStack();

        public void RaiseRuntimeError(LispToken problemToken, RuntimeErrorType errType, string )
        {
            throw new NotImplementedException();
        }
    }
}
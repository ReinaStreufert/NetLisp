using NetLisp.Data;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public class CallStack
    {
        private Stack<CallTrace> stack = new Stack<CallTrace>();

        internal CallStack() { }

        public ExecutableLispToken? CurrentlyExecutingToken
        {
            get
            {
                if (stack.Count == 0) return null;
                return stack.Peek().CalledToken;
            }
        }
        public LispToken CurrentlyEvaluatingToken { get; internal set; }

        public void Push(ExecutableLispToken token)
        {
            stack.Push(new CallTrace(CurrentlyEvaluatingToken.SourceLocation, token));
        }
        public void Pop()
        {
            stack.Pop();
        }

        public IEnumerable<CallTrace> AllCallers()
        {
            return stack;
        }
        public CallStack Copy()
        {
            CallStack copy = new CallStack();
            foreach (CallTrace caller in AllCallers())
            {
                copy.stack.Push(caller);
            }
            return copy;
        }
    }
}

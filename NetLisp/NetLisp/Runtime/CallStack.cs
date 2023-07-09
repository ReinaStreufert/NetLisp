using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public class CallStack
    {
        // TODO: reimpliment with source locations of the caller instead of just the routine called
        private Stack<ExecutableLispToken> stack = new Stack<ExecutableLispToken>();

        internal CallStack() { }

        public ExecutableLispToken? CurrentlyExecutingToken
        {
            get
            {
                if (stack.Count == 0) return null;
                return stack.Peek();
            }
        }

        public void Push(ExecutableLispToken token)
        {
            stack.Push(token);
        }
        public void Pop()
        {
            stack.Pop();
        }

        public IEnumerable<ExecutableLispToken> AllCallers()
        {
            return stack;
        }
    }
}

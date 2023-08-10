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
        public bool IsLoopFlagSet
        {
            get
            {
                if (stack.Count == 0) return false;
                return stack.Peek().LoopFlag;
            }
        }
        public LispList LoopCallTarget
        {
            get
            {
                if (!IsLoopFlagSet)
                {
                    throw new InvalidOperationException("Loop flag not set");
                }
                return stack.Peek().LoopCallTarget;
            }
        }
        public LispToken CurrentlyEvaluatingToken { get; set; }

        public void Push(ExecutableLispToken token)
        {
            stack.Push(new CallTrace(CurrentlyEvaluatingToken.SourceLocation, token));
        }
        public void Pop()
        {
            stack.Pop();
        }

        // set loop flag (runitback) on the first found function in the stack
        public bool SetLoopFlag(LispList callTarget)
        {
            foreach (CallTrace call in stack)
            {
                if (call.CalledToken.Type == LispDataType.Function)
                {
                    call.LoopFlag = true;
                    call.LoopCallTarget = callTarget;
                    return true;
                }
            }
            return false;
        }
        // clears loop flag on current caller
        public void ClearLoopFlag() // expected to be used correctly (check loopflag first)
        {
            stack.Peek().LoopFlag = false;
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

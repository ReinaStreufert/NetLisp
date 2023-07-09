using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public class ScopeStack
    {
        private Stack<Scope> stack = new Stack<Scope>();

        internal ScopeStack()
        {
            stack.Push(Scope.CreateGlobal());
        }

        public Scope GlobalScope
        {
            get
            {
                return stack.First();
            }
        }
        public Scope CurrentScope
        {
            get
            {
                return stack.Peek();
            }
        }

        public void Push()
        {
            stack.Push(Scope.CreateInheritingScope(CurrentScope));
        }
        public void Pop()
        {
            stack.Pop();
        }

        public IEnumerable<Scope> AllScopes()
        {
            return stack;
        }
    }
}

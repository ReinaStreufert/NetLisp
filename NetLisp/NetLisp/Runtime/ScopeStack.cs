using NetLisp.Data;
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
        internal ScopeStack(Scope global)
        {
            stack.Push(global);
        }

        public static ScopeStack ConstructFromScope(Scope scope)
        {
            List<Scope> scopes = new List<Scope>();
            scopes.Add(scope);
            while (scope.Parent != null)
            {
                scope = scope.Parent;
                scopes.Add(scope);
            }
            scopes.Reverse();
            Stack<Scope> constructedStack = new Stack<Scope>();
            foreach (Scope stackScope in scopes)
            {
                constructedStack.Push(stackScope);
            }
            ScopeStack result = new ScopeStack();
            result.stack = constructedStack;
            return result;
        }

        public Scope GlobalScope
        {
            get
            {
                return stack.Last();
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
        public ScopeStack Copy()
        {
            ScopeStack copy = new ScopeStack();
            foreach (Scope scope in AllScopes())
            {
                copy.stack.Push(scope);
            }
            return copy;
        }
    }
}

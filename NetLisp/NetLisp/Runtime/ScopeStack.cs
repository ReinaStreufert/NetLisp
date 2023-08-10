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
        public static ScopeStack Combine(ScopeStack scopeStack1, ScopeStack scopeStack2)
        {
            ScopeStack combined = new ScopeStack();
            combined.stack.Clear();
            IEnumerator<Scope> scopeStack1Enum = scopeStack1.AllScopes().Reverse().GetEnumerator();
            bool stack1End = false;
            IEnumerator<Scope> scopeStack2Enum = scopeStack2.AllScopes().Reverse().GetEnumerator();
            bool stack2End = false;

            while (!stack1End || !stack2End)
            {
                if (!stack1End)
                {
                    if (scopeStack1Enum.MoveNext())
                    {
                        Scope scope = scopeStack1Enum.Current;
                        if (combined.stack.Count > 0)
                        {
                            Scope linkedScope = Scope.CreateInheritingScope(combined.CurrentScope);
                            Scope.LinkScopes(scope, linkedScope);
                            combined.stack.Push(linkedScope);
                        }
                        else
                        {
                            combined.stack.Push(scopeStack1.GlobalScope);
                        }
                    } else
                    {
                        stack1End = true;
                    }
                }
                if (!stack2End)
                {
                    if (scopeStack2Enum.MoveNext())
                    {
                        Scope scope = scopeStack2Enum.Current;
                        Scope linkedScope = Scope.CreateInheritingScope(combined.CurrentScope);
                        Scope.LinkScopes(scope, linkedScope);
                        combined.stack.Push(linkedScope);
                    } else
                    {
                        stack2End = true;
                    }
                }
            }
            return combined;
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
            foreach (Scope scope in AllScopes().Reverse())
            {
                copy.stack.Push(scope);
            }
            return copy;
        }
    }
}

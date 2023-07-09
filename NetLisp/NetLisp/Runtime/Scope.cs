using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public class Scope
    {
        private Dictionary<string, LispToken> nameTable = new Dictionary<string, LispToken>();

        public Scope Parent { get; set; } = null;

        private Scope() { }
        internal static Scope CreateGlobal()
        {
            return new Scope();
        }
        internal static Scope CreateInheritingScope(Scope parent)
        {
            return new Scope() { Parent = parent };
        }

        public bool Define(string name)
        {
            return Define(name, null);
        }
        public bool Define(string name, LispToken initValue)
        {
            if (nameTable.ContainsKey(name))
            {
                return false;
            }
            nameTable.Add(name, initValue);
            return true;
        }
        public LispToken? Get(string name)
        {
            if (!nameTable.ContainsKey(name))
            {
                if (Parent != null)
                {
                    return Parent.Get(name);
                } else
                {
                    return null;
                }
            } else
            {
                return nameTable[name];
            }
        }
        public bool Set(string name, LispToken value)
        {
            if (!nameTable.ContainsKey(name))
            {
                if (Parent != null)
                {
                    return Parent.Set(name, value);
                } else
                {
                    return false;
                }
            } else
            {
                nameTable[name] = value;
                return true;
            }
        }
        // likely slow and should only be used for expressing the call stack during a runtime error
        public string? Search(LispToken value)
        {
            foreach (KeyValuePair<string, LispToken> pair in nameTable)
            {
                if (pair.Value == value)
                {
                    return pair.Key;
                }
            }
            if (Parent == null)
            {
                return null;
            } else
            {
                return Parent.Search(value);
            }
        }
    }
}

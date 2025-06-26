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

        private Scope? parent = null;
        public Scope? Parent
        {
            get { return parent; }
            set { parent = value; }
        }

        private Scope() { }
        internal static Scope CreateGlobal()
        {
            return new Scope();
        }
        internal static Scope CreateInheritingScope(Scope parent)
        {
            return new Scope() { parent = parent };
        }

        public static void LinkScopes(Scope srcScope, Scope dstScope)
        {
            dstScope.nameTable = srcScope.nameTable;
        }

        public bool Define(string name)
        {
            return Define(name, null);
        }
        public bool Define(string name, LispToken initValue)
        {
            return nameTable.TryAdd(name, initValue);
        }
        public LispToken? Get(string name)
        {
            LispToken nameVal;
            if (!nameTable.TryGetValue(name, out nameVal))
            {
                if (parent != null)
                {
                    return parent.Get(name);
                } else
                {
                    return null;
                }
            } else
            {
                return nameVal;
            }
        }
        public bool Set(string name, LispToken value)
        {
            if (!nameTable.ContainsKey(name))
            {
                if (parent != null)
                {
                    return parent.Set(name, value);
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
            if (parent == null)
            {
                return null;
            } else
            {
                return parent.Search(value);
            }
        }
        public IEnumerable<KeyValuePair<string, LispToken>> AllDefinedNames()
        {
            return nameTable;
        }
    }
}

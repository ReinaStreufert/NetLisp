using NetLisp.Data;
using NetLisp.Structs;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LispTableNativeSource
{
    public class LispTable : ExtendedLispToken, IEnumerable<KeyValuePair<LispToken, LispToken>>, IEnumerable
    {
        private class LispTokenEqualityComparer : IEqualityComparer<LispToken>
        {
            public bool Equals(LispToken a, LispToken b)
            {
                return (a.CompareValue(b));
            }
            public int GetHashCode(LispToken val)
            {
                return val.HashValue();
            }
        }

        public static ExtendedTypeInfo TableExtendedTypeInfo { get; } = new ExtendedTypeInfo()
        {
            ExtendedTypeGuid = new Guid("3347cb5b-f285-4b9a-873c-fbf3fe480eb3"),
            ExtendedTypeName = "table"
        };

        public override ExtendedTypeInfo ExtendedTypeInfo => LispTable.TableExtendedTypeInfo;

        private ConcurrentDictionary<LispToken, LispToken> dictionary = new ConcurrentDictionary<LispToken, LispToken>(new LispTokenEqualityComparer());

        public LispToken? this[LispToken key]
        {
            get
            {
                if (dictionary.ContainsKey(key))
                {
                    return dictionary[key];
                } else
                {
                    return null;
                }
            }
            set
            {
                dictionary[key] = value;
            }
        }
        public bool ContainsKey(LispToken key)
        {
            return dictionary.ContainsKey(key);
        }
        public bool DeleteKey(LispToken key)
        {
            LispToken trash;
            return dictionary.TryRemove(key, out trash);
        }

        public override bool CompareValue(LispToken token)
        {
            return this == token; // reference
        }

        public override int HashValue()
        {
            return GetHashCode(); // reference
        }

        public IEnumerator<KeyValuePair<LispToken, LispToken>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }
    }
}

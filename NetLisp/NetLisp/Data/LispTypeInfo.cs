using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Data
{
    // created to provide unified type info for simple lisp types and extended lisp types
    // this probably should been written a long time ago. like before i wrote a million of:
    // if (token.Type == LispDataType.ExtendedType) { (LispExtendedType)token... } else { ... }
    // that now looks very dumb to be there since i now have this nice class to do it for you
    public abstract class LispTypeInfo
    {
        public static LispTypeInfo FromInstance(LispToken token)
        {
            if (token.Type == LispDataType.ExtendedType)
            {
                return new ExtendedLispTypeInfo(((ExtendedLispToken)token).ExtendedTypeInfo);
            } else
            {
                return new SimpleLispTypeInfo(token.Type);
            }
        }
        public static LispTypeInfo FromSimpleType(LispDataType type)
        {
            return new SimpleLispTypeInfo(type);
        }
        public static LispTypeInfo FromExtendedTypeInfo(ExtendedTypeInfo type)
        {
            return new ExtendedLispTypeInfo(type);
        }
        // WARNING: Extended types produced by this function will not have their usual friendly names,
        // instead the friendly name and typestr will be equal. dont try to use this in situations where
        // you need a user-friendly type name. it is perfectly valid and safe for comparison use.
        public static bool TryParseFromTypeStr(string typestr, out LispTypeInfo result)
        {
            Guid guid;
            if (Guid.TryParseExact(typestr, "D", out guid))
            {
                result = FromExtendedTypeInfo(new ExtendedTypeInfo() { ExtendedTypeName = typestr, ExtendedTypeGuid = guid });
                return true;
            }
            object parseResult;
            if (Enum.TryParse(typeof(LispDataType), typestr, true, out parseResult))
            {
                result = FromSimpleType((LispDataType)parseResult);
                return true;
            }
            result = null;
            return false;
        }

        public abstract string TypeStr { get; }
        public abstract string FriendlyName { get; }
        public abstract bool IsExtendedType { get; }

        public bool Compare(LispTypeInfo type)
        {
            if (type.IsExtendedType)
            {
                return Compare(((ExtendedLispTypeInfo)type).ExtendedType);
            } else
            {
                return Compare(((SimpleLispTypeInfo)type).Type);
            }
        }
        public abstract bool Compare(LispDataType type);
        public abstract bool Compare(ExtendedTypeInfo type);

        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            } else if (obj.GetType() == typeof(LispTypeInfo))
            {
                return Compare((LispTypeInfo)obj);
            } else if (obj.GetType() == typeof(LispDataType))
            {
                return Compare((LispDataType)obj);
            } else if (obj.GetType() == typeof(ExtendedTypeInfo))
            {
                return Compare((ExtendedTypeInfo)obj);
            } else
            {
                return false;
            }
        }

        public static bool operator ==(LispTypeInfo a, LispTypeInfo b)
        {
            return a.Compare(b);
        }
        public static bool operator !=(LispTypeInfo a, LispTypeInfo b)
        {
            return !a.Compare(b);
        }
        public static bool operator ==(LispTypeInfo a, LispDataType b)
        {
            return a.Compare(b);
        }
        public static bool operator !=(LispTypeInfo a, LispDataType b)
        {
            return !a.Compare(b);
        }
        public static bool operator ==(LispTypeInfo a, ExtendedTypeInfo b)
        {
            return a.Compare(b);
        }
        public static bool operator !=(LispTypeInfo a, ExtendedTypeInfo b)
        {
            return !a.Compare(b);
        }

        private class SimpleLispTypeInfo : LispTypeInfo
        {
            public LispDataType Type { get; set; }
            public SimpleLispTypeInfo(LispDataType type)
            {
                Type = type;
            }

            public override string FriendlyName => Type.ToString().ToLower();
            public override string TypeStr => FriendlyName;
            public override bool IsExtendedType => false;

            public override bool Compare(LispDataType type)
            {
                return Type == type;
            }
            public override bool Compare(ExtendedTypeInfo type) => false;
        }
        private class ExtendedLispTypeInfo : LispTypeInfo
        {
            public ExtendedTypeInfo ExtendedType { get; set; }
            public ExtendedLispTypeInfo(ExtendedTypeInfo extendedType)
            {
                ExtendedType = extendedType;
            }

            public override string FriendlyName => ExtendedType.ExtendedTypeName;
            public override string TypeStr => ExtendedType.ExtendedTypeGuid.ToString("D");
            public override bool IsExtendedType => true;

            public override bool Compare(LispDataType type) => false;
            public override bool Compare(ExtendedTypeInfo type)
            {
                return ExtendedType.ExtendedTypeGuid == type.ExtendedTypeGuid;
            }
        }
    }
    public class LispTypeInfoEqualityComparer : IEqualityComparer<LispTypeInfo>
    {
        public static LispTypeInfoEqualityComparer Comparer { get; } = new LispTypeInfoEqualityComparer();

        public bool Equals(LispTypeInfo? x, LispTypeInfo? y)
        {
            if (x == (object)null || y == (object)null)
            {
                return (x == (object)null && y == (object)null);
            }
            return x == y;
        }

        public int GetHashCode([DisallowNull] LispTypeInfo obj)
        {
            return obj.GetHashCode();
        }
    }
}

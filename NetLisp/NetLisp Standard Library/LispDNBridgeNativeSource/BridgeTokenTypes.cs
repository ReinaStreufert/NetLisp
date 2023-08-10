using NetLisp.Data;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LispDNBridgeNativeSource
{
    // represents a dotnet type as a LispToken in the nlisp-dotnet bridge
    public class DotnetType : ExtendedLispToken
    {
        public static ExtendedTypeInfo DotnetTypeExtendedTypeInfo { get; } = new ExtendedTypeInfo()
        {
            ExtendedTypeGuid = new Guid("b3f104b5-3b72-4ef7-8e96-eb77ae2e690b"),
            ExtendedTypeName = "dotnet-type"
        };

        public override ExtendedTypeInfo ExtendedTypeInfo => DotnetType.DotnetTypeExtendedTypeInfo;

        public Type TypeInstance { get; set; }

        public DotnetType(Type type)
        {
            TypeInstance = type;
        }

        public override bool CompareValue(LispToken token)
        {
            if (token.Type == LispDataType.ExtendedType && ((ExtendedLispToken)token).ExtendedTypeInfo.ExtendedTypeGuid == DotnetTypeExtendedTypeInfo.ExtendedTypeGuid)
            {
                return ((DotnetType)token).TypeInstance == TypeInstance;
            } else
            {
                return false;
            }
        }

        public override int HashValue()
        {
            return TypeInstance.GetHashCode() + 1;
        }
    }
    // represents a dotnet instance as a LispToken in the nlisp-dotnet bridge
    public class DotnetInstance : ExtendedLispToken
    {
        public static ExtendedTypeInfo DotnetInstanceExtendedTypeInfo { get; } = new ExtendedTypeInfo()
        {
            ExtendedTypeGuid = new Guid("ecf19b78-659c-4f4b-9fbd-41fb83f7843d"),
            ExtendedTypeName = "dotnet-object"
        };

        public override ExtendedTypeInfo ExtendedTypeInfo => DotnetInstance.DotnetInstanceExtendedTypeInfo;

        public object Instance { get; set; }

        public DotnetInstance(object instance)
        {
            Instance = instance;
        }

        public override bool CompareValue(LispToken token)
        {
            if (token.Type == LispDataType.ExtendedType && ((ExtendedLispToken)token).ExtendedTypeInfo.ExtendedTypeGuid == DotnetInstanceExtendedTypeInfo.ExtendedTypeGuid)
            {
                return ((DotnetInstance)token).Instance == Instance;
            }
            else
            {
                return false;
            }
        }

        public override int HashValue()
        {
            return Instance.GetHashCode() + 1;
        }
    }
}

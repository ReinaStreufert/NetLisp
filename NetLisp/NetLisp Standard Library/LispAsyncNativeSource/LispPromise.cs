using NetLisp.Data;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LispAsyncNativeSource
{
    public class LispPromise : ExtendedLispToken
    {
        public static ExtendedTypeInfo PromiseExtendedTypeInfo { get; } = new ExtendedTypeInfo()
        {
            ExtendedTypeGuid = new Guid("26aeaa4a-7248-4915-86ad-392f1eca77c4"),
            ExtendedTypeName = "promise"
        };

        public override ExtendedTypeInfo ExtendedTypeInfo => PromiseExtendedTypeInfo;

        public Task<LispToken[]> Task { get; set; }

        public LispPromise(Task<LispToken[]> task)
        {
            Task = task;
        }

        public override bool CompareValue(LispToken token)
        {
            if (token.Type == LispDataType.ExtendedType)
            {
                if (((ExtendedLispToken)token).ExtendedTypeInfo.ExtendedTypeGuid == PromiseExtendedTypeInfo.ExtendedTypeGuid)
                {
                    return (Task == ((LispPromise)token).Task);
                } else
                {
                    return false;
                }
            } else
            {
                return false;
            }
        }

        public override int HashValue()
        {
            return Task.GetHashCode();
        }
    }
}

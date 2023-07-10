using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public abstract class ExecutableBody
    {
        public abstract ExecutableBodyType Type { get; }
        public abstract IEnumerable<LispToken> Execute(RuntimeContext runtimeContext);
    }
    public class LispExecutableBody : ExecutableBody
    {
        public override ExecutableBodyType Type => ExecutableBodyType.LispDefinedBody;

        public LispList Expression { get; set; }

        public LispExecutableBody(LispList expression)
        {
            Expression = expression;
        }

        public override IEnumerable<LispToken> Execute(RuntimeContext runtimeContext)
        {
            IEnumerable<LispToken> returnVals = Expression.Evaluate(runtimeContext);
            foreach (LispToken returnVal in returnVals)
            {
                yield return returnVal;
            }
        }
    }
    public delegate IEnumerable<LispToken> LispFunctionNativeCallback(RuntimeContext runtimeContext);
    public class NativeExecutableBody : ExecutableBody
    {
        public override ExecutableBodyType Type => ExecutableBodyType.NativeBody;

        public LispFunctionNativeCallback NativeCallback { get; }

        public NativeExecutableBody(LispFunctionNativeCallback nativeCallback)
        {
            NativeCallback = nativeCallback;
        }

        public override IEnumerable<LispToken> Execute(RuntimeContext runtimeContext)
        {
            return NativeCallback(runtimeContext);
        }
    }
    public enum ExecutableBodyType
    {
        LispDefinedBody,
        NativeBody
    }
}

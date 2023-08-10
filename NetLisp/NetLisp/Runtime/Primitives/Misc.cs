using NetLisp.Data;
using NetLisp.Data.SpecialTokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    class PrintStr : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "printstr takes 1 argument");
            }
            Console.WriteLine(runtimeContext.Assert<LispString>(passedArgs[0], LispDataType.String).Value);
            yield break;
        }
    }
    class Values : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            foreach (LispToken value in passedArgs)
            {
                yield return value;
            }
        }
    }
    class ReturnValues : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            LispReturnTuple returnTuple = new LispReturnTuple();
            foreach (LispToken arg in passedArgs)
            {
                returnTuple.ReturnValues.Add(arg);
            }
            yield return returnTuple;
            yield break;
        }
    }
    class Quote : LispSpecialForm // although ' should mainly be used for quoting, the quote function may be required such as in macros
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            foreach (LispToken arg in passedArgs)
            {
                yield return arg;
            }
        }
    }
    class TypeStr : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "typestr takes 1 argument");
            }
            LispToken arg = passedArgs[0];
            yield return new LispString(arg.GetTypeInfo().TypeStr);
        }
    }
}

using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    abstract class MathematicOperator : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected abstract float Operation(float a, float b);

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            float resultVal = 0;
            bool firstVal = true;
            foreach (LispToken token in passedArgs)
            {
                if (token.Type != LispDataType.Number)
                {
                    runtimeContext.RaiseRuntimeError(token, RuntimeErrorType.ArgumentMismatchError, "Expected number after evaluation. Got " + token.Type.ToString().ToLower());
                }
                else
                {
                    LispNumber num = (LispNumber)token;
                    if (firstVal)
                    {
                        resultVal = num.Value;
                        firstVal = false;
                    } else
                    {
                        resultVal = Operation(resultVal, ((LispNumber)token).Value);
                    }
                }
            }
            yield return new LispNumber(resultVal);
        }
    }
    class PlusOperator : MathematicOperator
    {
        protected override float Operation(float a, float b)
        {
            return a + b;
        }
    }
    class MinusOperator : MathematicOperator
    {
        protected override float Operation(float a, float b)
        {
            return a - b;
        }
    }
    class TimesOperator : MathematicOperator
    {
        protected override float Operation(float a, float b)
        {
            return a * b;
        }
    }
    class DivideOperator : MathematicOperator
    {
        protected override float Operation(float a, float b)
        {
            return a / b;
        }
    }
}

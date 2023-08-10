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

        protected abstract double Operation(double a, double b);

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            double resultVal = 0;
            bool firstVal = true;
            foreach (LispToken token in passedArgs)
            {
                LispNumber num = runtimeContext.Assert<LispNumber>(token, LispDataType.Number);
                if (firstVal)
                {
                    resultVal = num.Value;
                    firstVal = false;
                }
                else
                {
                    resultVal = Operation(resultVal, ((LispNumber)token).Value);
                }
            }
            yield return new LispNumber(resultVal);
        }
    }
    class PlusOperator : MathematicOperator
    {
        protected override double Operation(double a, double b)
        {
            return a + b;
        }
    }
    class MinusOperator : MathematicOperator
    {
        protected override double Operation(double a, double b)
        {
            return a - b;
        }
    }
    class TimesOperator : MathematicOperator
    {
        protected override double Operation(double a, double b)
        {
            return a * b;
        }
    }
    class DivideOperator : MathematicOperator
    {
        protected override double Operation(double a, double b)
        {
            return a / b;
        }
    }
}

using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    abstract class LogicalOperator : LispSpecialForm
    {
        protected abstract bool IsTypeLocked { get; }
        protected abstract LispDataType TypeRequirement { get; }
        protected abstract bool LogicalFunction(LispToken a, LispToken b);
        protected virtual bool AllFalseMode { get => false; }

        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count < 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "Logical operator requires at least 2 arguments");
            }
            if (IsTypeLocked)
            {
                LispDataType typeRequirement = TypeRequirement;
                foreach (LispToken arg in passedArgs)
                {
                    runtimeContext.Assert<LispToken>(arg, typeRequirement);
                }
            }
            LispToken lastValue = passedArgs[0];
            for (int i = 1; i < passedArgs.Count; i++)
            {
                LispToken nextValue = passedArgs[i];
                if (AllFalseMode)
                {
                    if (LogicalFunction(lastValue, nextValue))
                    {
                        yield return new LispBoolean(true);
                        yield break;
                    }
                } else
                {
                    if (!LogicalFunction(lastValue, nextValue))
                    {
                        yield return new LispBoolean(false);
                        yield break;
                    }
                }
                lastValue = nextValue;
            }
            yield return new LispBoolean(!AllFalseMode);
        }
    }
    class Equals : LogicalOperator
    {
        protected override bool IsTypeLocked => false;
        protected override LispDataType TypeRequirement => throw new InvalidOperationException("not type locked");

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            return a.CompareValue(b);
        }
    }
    class GreaterThan : LogicalOperator
    {
        protected override bool IsTypeLocked => true;
        protected override LispDataType TypeRequirement => LispDataType.Number;

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            double aValue = ((LispNumber)a).Value;
            double bValue = ((LispNumber)b).Value;
            return aValue > bValue;
        }
    }
    class LessThan : LogicalOperator
    {
        protected override bool IsTypeLocked => true;
        protected override LispDataType TypeRequirement => LispDataType.Number;

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            double aValue = ((LispNumber)a).Value;
            double bValue = ((LispNumber)b).Value;
            return aValue < bValue;
        }
    }
    class Not : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "Unary logical operator takes exactly 1 argument");
            }
            LispBoolean arg = runtimeContext.Assert<LispBoolean>(passedArgs[0], LispDataType.Boolean);
            yield return new LispBoolean(!arg.Value);
        }
    }
    class Or : LogicalOperator
    {
        protected override bool IsTypeLocked => true;
        protected override LispDataType TypeRequirement => LispDataType.Boolean;
        protected override bool AllFalseMode => true;

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            bool aValue = ((LispBoolean)a).Value;
            bool bValue = ((LispBoolean)b).Value;
            return aValue || bValue;
        }
    }
    class And : LogicalOperator
    {
        protected override bool IsTypeLocked => true;
        protected override LispDataType TypeRequirement => LispDataType.Boolean;

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            bool aValue = ((LispBoolean)a).Value;
            bool bValue = ((LispBoolean)b).Value;
            return aValue && bValue;
        }
    }
}

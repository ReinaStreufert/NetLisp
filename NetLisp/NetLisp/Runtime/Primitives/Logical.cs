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
                    if (arg.Type != typeRequirement)
                    {
                        runtimeContext.RaiseRuntimeError(arg, RuntimeErrorType.ArgumentMismatchError, "Expected " + typeRequirement.ToString().ToLower() + " after evaluation. Got " + arg.Type.ToString().ToLower());
                    }
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
            if (a.Type != b.Type)
            {
                return false;
            }
            return compareTokens(a, b);
        }
        private bool compareTokens(LispToken a, LispToken b)
        {
            LispDataType type = a.Type;
            if (type == LispDataType.List)
            {
                return compareLists((LispList)a, (LispList)b);
            } else if (type == LispDataType.Symbol)
            {
                return compareSymbols((LispSymbol)a, (LispSymbol)b);
            } else if (type == LispDataType.Number)
            {
                return compareNumbers((LispNumber)a, (LispNumber)b);
            } else if (type == LispDataType.Boolean)
            {
                return compareBooleans((LispBoolean)a, (LispBoolean)b);
            } else
            {
                return a == b; // reference compare for all non-value tokens (functions, macros, etc.)
            }
        }
        private bool compareLists(LispList a, LispList b)
        {
            List<LispToken> aItems = a.Items;
            List<LispToken> bItems = b.Items;
            if (aItems.Count != bItems.Count)
            {
                return false;
            }
            for (int i = 0; i < aItems.Count; i++)
            {
                if (!compareTokens(aItems[i], bItems[i]))
                {
                    return false;
                }
            }
            return true;
        }
        private bool compareSymbols(LispSymbol a, LispSymbol b)
        {
            return (a.Value == b.Value);
        }
        private bool compareNumbers(LispNumber a, LispNumber b)
        {
            return (a.Value == b.Value);
        }
        private bool compareBooleans(LispBoolean a, LispBoolean b)
        {
            return (a.Value == b.Value);
        }
    }
    class GreaterThan : LogicalOperator
    {
        protected override bool IsTypeLocked => true;
        protected override LispDataType TypeRequirement => LispDataType.Number;

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            float aValue = ((LispNumber)a).Value;
            float bValue = ((LispNumber)b).Value;
            return aValue > bValue;
        }
    }
    class LessThan : LogicalOperator
    {
        protected override bool IsTypeLocked => true;
        protected override LispDataType TypeRequirement => LispDataType.Number;

        protected override bool LogicalFunction(LispToken a, LispToken b)
        {
            float aValue = ((LispNumber)a).Value;
            float bValue = ((LispNumber)b).Value;
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
            LispToken arg = passedArgs[0];
            if (arg.Type != LispDataType.Boolean)
            {
                runtimeContext.RaiseRuntimeError(arg, RuntimeErrorType.ArgumentMismatchError, "Expected boolean after evaluation. Got " + arg.Type.ToString().ToLower());
            }
            yield return new LispBoolean(!((LispBoolean)arg).Value);
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

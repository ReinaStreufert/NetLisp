using NetLisp.Data;
using NetLisp.Data.SpecialTokens;
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

        public LispList Expressions { get; set; }

        public LispExecutableBody(LispList expressions)
        {
            expressions.IsRoutineExpressionList = true;
            Expressions = expressions;
        }

        public override IEnumerable<LispToken> Execute(RuntimeContext runtimeContext)
        {
            IEnumerable<LispToken> evaluated = evaluateAllExpressions(runtimeContext);
            LispToken lastToken = null;
            foreach (LispToken evaluatedToken in evaluated)
            {
                if (evaluatedToken.IsImmediateReturnToken)
                {
                    yield return evaluatedToken;
                    yield break;
                }
                // check to make sure all but last evaluation result are not special tokens
                if (lastToken != null && lastToken.Type == LispDataType.SpecialToken)
                {
                    // try to evaluate it to a normal token, or runtime error depending on the
                    // SpecialToken's evaluate implementation
                    foreach (LispToken evaluatedSpecialToken in lastToken.Evaluate(runtimeContext))
                    {
                        lastToken = evaluatedSpecialToken;
                    }
                } else
                {
                    lastToken = evaluatedToken;
                }
            }
            if (lastToken == null)
            {
                yield break; // empty return
            }
            // determine if last token is output from returnvalues
            if (lastToken.Type == LispDataType.SpecialToken)
            {
                SpecialLispToken lastTokenSpecial = (SpecialLispToken)lastToken;
                if (lastTokenSpecial.SpecialType == LispSpecialType.ReturnTuple)
                {
                    LispReturnTuple returnTuple = (LispReturnTuple)lastTokenSpecial;
                    foreach (LispToken returnValue in returnTuple.ReturnValues)
                    {
                        yield return returnValue;
                    }
                    yield break;
                } else
                {
                    foreach (LispToken token in lastTokenSpecial.Evaluate(runtimeContext))
                    {
                        yield return token;
                    }
                    yield break;
                }
            }
            yield return lastToken;
        }
        private IEnumerable<LispToken> evaluateAllExpressions(RuntimeContext runtimeContext)
        {
            foreach (LispToken expr in Expressions.Items)
            {
                if (expr.Quoted)
                {
                    yield return expr;
                    continue;
                }
                IEnumerable<LispToken> evaluationResult = expr.Evaluate(runtimeContext);
                foreach (LispToken val in evaluationResult)
                {
                    yield return val;
                }
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

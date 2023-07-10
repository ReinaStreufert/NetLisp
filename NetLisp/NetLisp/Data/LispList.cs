using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetLisp.Runtime;
using NetLisp.Text;

namespace NetLisp.Data
{
    public class LispList : LispToken
    {
        public override LispDataType Type => LispDataType.List;
        public override bool TypeCanBeExecuted => false;

        public List<LispToken> Items { get; set; } = new List<LispToken>();

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            if (Items.Count == 0)
            {
                yield return new LispList();
                yield break;
            }
            List<LispToken> evaluationResult = new List<LispToken>();
            LispToken firstItem = Items[0];
            LispToken firstItemEvaluated = null;
            if (!firstItem.Quoted)
            {
                // evaluate only the first item first as it may indicate that the rest should not be evaluated.
                runtimeContext.Calls.CurrentlyEvaluatingToken = firstItem;
                IEnumerable<LispToken> firstItemReturns = firstItem.Evaluate(runtimeContext);
                foreach (LispToken item in firstItemReturns)
                {
                    evaluationResult.Add(item);
                }
                if (evaluationResult.Count > 0)
                {
                    firstItemEvaluated = evaluationResult[0];
                } else
                {
                    firstItemEvaluated = null;
                }
            } else
            {
                firstItem.Quoted = false;
                evaluationResult.Add(firstItem);
                firstItemEvaluated = firstItem;
            }
            // evaluate rest of items if necessary
            if (firstItemEvaluated != null && !shouldEvaluateArgs(firstItemEvaluated))
            {
                for (int i = 1; i < Items.Count; i++)
                {
                    evaluationResult.Add(Items[i]);
                }
            } else
            {
                for (int i = 1; i < Items.Count; i++)
                {
                    LispToken item = Items[i];
                    runtimeContext.Calls.CurrentlyEvaluatingToken = item;
                    if (item.Quoted)
                    {
                        item.Quoted = false;
                        evaluationResult.Add(item);
                    }
                    else
                    {
                        IEnumerable<LispToken> evaluatedItemResult = item.Evaluate(runtimeContext);
                        foreach (LispToken evalutedItem in evaluatedItemResult)
                        {
                            evaluationResult.Add(evalutedItem);
                        }
                    }
                }
            }
            runtimeContext.Calls.CurrentlyEvaluatingToken = this;
            LispList evaluatedList = new LispList();
            evaluatedList.SourceLocation = SourceLocation;
            evaluatedList.Items = evaluationResult;
            if (firstItemEvaluated != null && firstItemEvaluated.TypeCanBeExecuted)
            {
                // execute the list as a function
                ExecutableLispToken executableToken = (ExecutableLispToken)firstItemEvaluated;
                IEnumerable<LispToken> executionResults = executableToken.Call(evaluatedList, runtimeContext);
                if (firstItemEvaluated.Type == LispDataType.Macro)
                {
                    // macros generally output "code" that needs to be further evaluated
                    foreach (LispToken executionResult in executionResults)
                    {
                        if (executionResult.Quoted)
                        {
                            executionResult.Quoted = false;
                            yield return executionResult;
                        } else
                        {
                            IEnumerable<LispToken> evaluatedExecutionResults = executionResult.Evaluate(runtimeContext);
                            foreach (LispToken evaluatedExecutionResult in evaluatedExecutionResults)
                            {
                                yield return evaluatedExecutionResult;
                            }
                        }
                    }
                    yield break;
                }
                foreach (LispToken executionResult in executionResults)
                {
                    yield return executionResult;
                }
            } else
            {
                // list does not require execution
                yield return evaluatedList;
            }
        }
        private bool shouldEvaluateArgs(LispToken firstItemEvaluated)
        {
            if (firstItemEvaluated.Type == LispDataType.Macro)
            {
                return false;
            } else if (firstItemEvaluated.Type == LispDataType.SpecialForm)
            {
                return ((LispSpecialForm)firstItemEvaluated).EvaluateArguments;
            } else
            {
                return true;
            }
        }

        public static bool TryParse(string input, out LispList result, out SyntaxError error, string sourceName = "") // return value decides which out param is set to null
        {
            LispListParser listParser = new LispListParser(input, sourceName);
            TokenParseResult lastParseResult = TokenParseResult.Success;
            while (lastParseResult == TokenParseResult.Success)
            {
                lastParseResult = listParser.ParseNext();
            }
            if (lastParseResult == TokenParseResult.EndOfInput)
            {
                result = listParser.ParseResult;
                error = null;
                return true;
            } else if (lastParseResult == TokenParseResult.EndOfExpression) 
            {
                // not valid for there to be more expressions in the input when parsing a single list
                result = null;
                SyntaxError err = new SyntaxError();
                err.ErrorLocation = listParser.GetLocation();
                err.ErrorType = SyntaxErrorType.ExpectedEndOfInput;
                err.Text = "Attempt to parse multiple S-Expressions as single list";
                error = err;
                return false;
            } else if (lastParseResult == TokenParseResult.SyntaxError)
            {
                result = null;
                error = listParser.LastError;
                return true;
            }
            // unreachable
            result = null;
            error = null;
            return false;
        }

        public override string ToString()
        {
            return LispTokenWriter.ListToString(Items);
        }
    }
}

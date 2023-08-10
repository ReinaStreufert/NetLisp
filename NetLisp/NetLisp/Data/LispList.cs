using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        internal bool IsRoutineExpressionList { get; set; } = false;

        public List<LispToken> Items { get; set; }

        public LispList()
        {
            Items = new List<LispToken>();
        }
        public LispList(List<LispToken> items)
        {
            Items = items;
        }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            if (Items.Count == 0)
            {
                yield return new LispList();
                yield break;
            }
            List<LispToken> evaluationResult = new List<LispToken>();
            LispToken firstItem = Items[0];
            if (firstItem.IsImmediateReturnToken)
            {
                yield return firstItem;
                yield break;
            }
            LispToken firstItemEvaluated = null;
            if (!firstItem.Quoted)
            {
                // evaluate only the first item first as it may indicate that the rest should not be evaluated.
                runtimeContext.Calls.CurrentlyEvaluatingToken = firstItem;
                IEnumerable<LispToken> firstItemReturns = firstItem.Evaluate(runtimeContext);
                foreach (LispToken item in firstItemReturns)
                {
                    if (item.IsImmediateReturnToken)
                    {
                        yield return item;
                        yield break;
                    }
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
                evaluationResult.Add(firstItem);
                firstItemEvaluated = firstItem;
            }
            if (firstItemEvaluated != null)
            {
                firstItemEvaluated = filterSpecialTokens(runtimeContext, firstItemEvaluated, false);
            }
            // evaluate rest of items if necessary
            if (firstItemEvaluated != null && !shouldEvaluateArgs(firstItemEvaluated))
            {
                for (int i = 1; i < Items.Count; i++)
                {
                    LispToken item = Items[i];
                    if (item.IsImmediateReturnToken)
                    {
                        yield return item;
                        yield break;
                    }
                    evaluationResult.Add(Items[i]);
                }
            } else
            {
                for (int i = 1; i < Items.Count; i++)
                {
                    LispToken item = Items[i];
                    if (item.IsImmediateReturnToken)
                    {
                        yield return item;
                        yield break;
                    }
                    runtimeContext.Calls.CurrentlyEvaluatingToken = item;
                    if (item.Quoted)
                    {
                        evaluationResult.Add(item);
                    }
                    else
                    {
                        IEnumerable<LispToken> evaluatedItemResult = item.Evaluate(runtimeContext);
                        LispToken lastEvaluatedItem = null;
                        foreach (LispToken evalutedItem in evaluatedItemResult)
                        {
                            if (evalutedItem.IsImmediateReturnToken)
                            {
                                yield return evalutedItem;
                                yield break;
                            }
                            if (firstItemEvaluated != null && firstItemEvaluated.Type == LispDataType.SpecialForm)
                            {
                                evaluationResult.Add(evalutedItem);
                            } else
                            {
                                if (lastEvaluatedItem != null)
                                {
                                    evaluationResult.Add(filterSpecialTokens(runtimeContext, lastEvaluatedItem, false));
                                }
                                lastEvaluatedItem = evalutedItem;
                            }
                        }
                        if (firstItemEvaluated == null || firstItemEvaluated.Type != LispDataType.SpecialForm)
                        {
                            if (lastEvaluatedItem != null)
                                evaluationResult.Add(filterSpecialTokens(runtimeContext, lastEvaluatedItem, (i >= Items.Count - 1)));
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
                    // they also cant tell if theyre going to evaluate to an immediate return in which
                    // case they will exit without popping the call stack (god that was impossible to
                    // debug). we must iterate the whole thing before processing the results
                    List<LispToken> macroResults = executionResults.ToList();
                    foreach (LispToken macroResult in macroResults)
                    {
                        if (macroResult.Quoted)
                        {
                            yield return macroResult;
                        } else
                        {
                            IEnumerable<LispToken> evaluatedExecutionResults = macroResult.Evaluate(runtimeContext);
                            foreach (LispToken evaluatedExecutionResult in evaluatedExecutionResults)
                            {
                                if (evaluatedExecutionResult.IsImmediateReturnToken)
                                {
                                    
                                    yield return evaluatedExecutionResult;
                                    yield break;
                                }
                                yield return evaluatedExecutionResult;
                            }
                        }
                    }
                    yield break;
                }
                LispToken lastExecutionResult = null;
                foreach (LispToken executionResult in executionResults)
                {
                    if (executionResult.IsImmediateReturnToken)
                    {
                        yield return executionResult;
                        yield break;
                    }
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
        private LispToken filterSpecialTokens(RuntimeContext runtimeContext, LispToken token, bool isLastItem)
        {
            if (token.Type == LispDataType.SpecialToken)
            {
                if (IsRoutineExpressionList && isLastItem)
                {
                    SpecialLispToken specialToken = (SpecialLispToken)token;
                    if (specialToken.SpecialType == LispSpecialType.ReturnTuple)
                    {
                        return specialToken;
                    } else
                    {
                        return specialToken.Evaluate(runtimeContext).First();
                    }
                } else
                {
                    return token.Evaluate(runtimeContext).First();
                }
            } else
            {
                return token;
            }
        }

        public override bool CompareValue(LispToken token)
        {
            if (token.Type != LispDataType.List)
            {
                return false;
            }
            return compareLists(this, (LispList)token);
        }
        public override int HashValue()
        {
            int hashCode = 1;
            foreach (LispToken item in Items)
            {
                hashCode = 31 * hashCode + (item == null ? 0 : item.HashValue());
            }
            return hashCode;
        }

        private static bool compareLists(LispList a, LispList b)
        {
            List<LispToken> aItems = a.Items;
            List<LispToken> bItems = b.Items;
            if (aItems.Count != bItems.Count)
            {
                return false;
            }
            for (int i = 0; i < aItems.Count; i++)
            {
                if (!aItems[i].CompareValue(bItems[i])) // if it is a list this will inevitably be recursive with no need to do any extra work
                {
                    return false;
                }
            }
            return true;
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

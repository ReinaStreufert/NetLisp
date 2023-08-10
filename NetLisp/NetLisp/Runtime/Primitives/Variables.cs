using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    static class VariableSpecialFormUtils
    {
        public static LispToken LastImmediateReturnToken = null;

        public delegate void AssignmentFunction(LispSymbol defName, LispToken? defValue);

        public static bool ExecuteAssignmentList(List<LispToken> assignmentList, RuntimeContext runtimeContext, AssignmentFunction assignmentFunction) // returns false in event of immediate return token
        {
            bool expectSymbol = true;
            LispSymbol lastDefinitionSymbol = null;
            foreach (LispToken arg in assignmentList)
            {
                if (expectSymbol)
                {
                    LispSymbol defSymbol = runtimeContext.Assert<LispSymbol>(arg, LispDataType.Symbol);
                    lastDefinitionSymbol = defSymbol;
                    expectSymbol = false;
                }
                else
                {
                    LispToken argEvaluated = null;
                    if (arg.Quoted)
                    {
                        arg.Quoted = false;
                        argEvaluated = arg;
                    }
                    else
                    {
                        IEnumerable<LispToken> evaluationResult = arg.Evaluate(runtimeContext);
                        foreach (LispToken token in evaluationResult)
                        {
                            if (argEvaluated == null)
                            {
                                argEvaluated = token;
                            }
                            else
                            {
                                runtimeContext.RaiseRuntimeError(arg, RuntimeErrorType.ExpectedSingleValue, "Value to assign evaluated to multiple values. Did you mean to quote it?");
                            }
                        }
                        if (argEvaluated != null)
                        {
                            if (argEvaluated.IsImmediateReturnToken)
                            {
                                LastImmediateReturnToken = argEvaluated;
                                return false;
                            } else if (argEvaluated.Type == LispDataType.SpecialToken)
                            {
                                argEvaluated = argEvaluated.Evaluate(runtimeContext).First();
                            }
                        }
                    }
                    assignmentFunction(lastDefinitionSymbol, argEvaluated);
                    expectSymbol = true;
                }
            }
            if (!expectSymbol)
            {
                assignmentFunction(lastDefinitionSymbol, null);
            }
            return true;
        }
    }
    class Define : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            bool immediateReturn = !VariableSpecialFormUtils.ExecuteAssignmentList(passedArgs, runtimeContext, (LispSymbol defSymbol, LispToken? defValue) =>
            {
                bool defResult;
                if (defValue == null)
                {
                    defResult = runtimeContext.Scopes.GlobalScope.Define(defSymbol.Value);
                } else
                {
                    defResult = runtimeContext.Scopes.GlobalScope.Define(defSymbol.Value, defValue);
                }
                if (!defResult)
                {
                    runtimeContext.RaiseRuntimeError(defSymbol, RuntimeErrorType.SymbolAlreadyDefined, "Cannot define '" + defSymbol.Value + "' in the global scope because it is already defined");
                }
            });
            if (immediateReturn)
            {
                yield return VariableSpecialFormUtils.LastImmediateReturnToken;
            }
        }
    }
    class Setq : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count % 2 > 0)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "'setq' requires an even number of arguments");
            }
            bool immediateReturn = !VariableSpecialFormUtils.ExecuteAssignmentList(passedArgs, runtimeContext, (LispSymbol varSymbol, LispToken? varValue) =>
            {
                if (!runtimeContext.Scopes.CurrentScope.Set(varSymbol.Value, varValue))
                {
                    runtimeContext.RaiseRuntimeError(varSymbol, RuntimeErrorType.UnknownSymbolMeaning, "'" + varSymbol.Value + "' is not a variable in the current scope or inheriting scopes");
                }
            });
            if (immediateReturn)
            {
                yield return VariableSpecialFormUtils.LastImmediateReturnToken;
            }
        }
    }
    class Let : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count != 2)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "'let' takes 2 arguments");
            }
            LispList defsList = runtimeContext.Assert<LispList>(passedArgs[0], LispDataType.List);
            LispList exprList = runtimeContext.Assert<LispList>(passedArgs[1], LispDataType.List);
            runtimeContext.Scopes.Push();
            bool immediateReturn = !VariableSpecialFormUtils.ExecuteAssignmentList(defsList.Items, runtimeContext, (LispSymbol defSymbol, LispToken defValue) =>
            {
                // since its a new scope and you can define over inheriting scopes this should always succeed
                if (defValue != null)
                {
                    runtimeContext.Scopes.CurrentScope.Define(defSymbol.ToString(), defValue);
                } else
                {
                    runtimeContext.Scopes.CurrentScope.Define(defSymbol.ToString());
                }
            });
            if (immediateReturn)
            {
                runtimeContext.Scopes.Pop();
                yield return VariableSpecialFormUtils.LastImmediateReturnToken;
            }
            LispExecutableBody letBody = new LispExecutableBody(exprList);
            IEnumerable<LispToken> result = letBody.Execute(runtimeContext);
            foreach (LispToken returnValue in result)
            {
                if (returnValue.IsImmediateReturnToken)
                {
                    runtimeContext.Scopes.Pop();
                    yield return returnValue;
                    yield break;
                }
                yield return returnValue;
            }
            runtimeContext.Scopes.Pop();
        }
    }
}

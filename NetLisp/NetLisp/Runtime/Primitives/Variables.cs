using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    class Define : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            bool expectSymbol = true;
            LispSymbol lastDefinitionSymbol = null;
            foreach (LispToken arg in passedArgs)
            {
                if (expectSymbol)
                {
                    if (arg.Type != LispDataType.Symbol)
                    {
                        runtimeContext.RaiseRuntimeError(arg, RuntimeErrorType.ArgumentMismatchError, "Expected symbol got " + arg.Type.ToString().ToLower());
                    }
                    LispSymbol defSymbol = (LispSymbol)arg;
                    lastDefinitionSymbol = defSymbol;
                    expectSymbol = false;
                } else
                {
                    LispToken argEvaluated = null;
                    if (arg.Quoted)
                    {
                        arg.Quoted = false;
                        argEvaluated = arg;
                    } else
                    {
                        IEnumerable<LispToken> evaluationResult = arg.Evaluate(runtimeContext);
                        foreach (LispToken token in evaluationResult)
                        {
                            if (argEvaluated == null)
                            {
                                argEvaluated = token;
                            } else
                            {
                                runtimeContext.RaiseRuntimeError(arg, RuntimeErrorType.ExpectedSingleValue, "Value to assign evaluated to multiple values. Did you mean to quote it?");
                            }
                        }
                    }
                    if (argEvaluated != null)
                    {
                        if (!runtimeContext.Scopes.GlobalScope.Define(lastDefinitionSymbol.Value, argEvaluated))
                        {
                            runtimeContext.RaiseRuntimeError(lastDefinitionSymbol, RuntimeErrorType.SymbolAlreadyDefined, "Cannot define symbol '" + lastDefinitionSymbol.Value + "' because it is already defined in the calling scope");
                        }
                    } else
                    {
                        if (!runtimeContext.Scopes.GlobalScope.Define(lastDefinitionSymbol.Value))
                        {
                            runtimeContext.RaiseRuntimeError(lastDefinitionSymbol, RuntimeErrorType.SymbolAlreadyDefined, "Cannot define symbol '" + lastDefinitionSymbol.Value + "' because it is already defined in the calling scope");
                        }
                    }
                    expectSymbol = true;
                }
            }
            if (!expectSymbol)
            {
                if (!runtimeContext.Scopes.GlobalScope.Define(lastDefinitionSymbol.Value))
                {
                    runtimeContext.RaiseRuntimeError(lastDefinitionSymbol, RuntimeErrorType.SymbolAlreadyDefined, "Cannot define symbol '" + lastDefinitionSymbol.Value + "' because it is already defined in the calling scope");
                }
            }
            yield break;
        }
    }
    class Setq : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (passedArgs.Count % 2 > 0)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "Setq requires an even number of arguments");
            }
            bool expectSymbol = true;
            LispSymbol lastVarSymbol = null;
            foreach (LispToken arg in passedArgs)
            {
                if (expectSymbol)
                {
                    if (arg.Type != LispDataType.Symbol)
                    {
                        runtimeContext.RaiseRuntimeError(arg, RuntimeErrorType.ArgumentMismatchError, "Expected symbol got " + arg.Type.ToString().ToLower());
                    }
                    LispSymbol varSymbol = (LispSymbol)arg;
                    lastVarSymbol = varSymbol;
                    expectSymbol = false;
                } else
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
                    }
                    if (!runtimeContext.Scopes.CurrentScope.Set(lastVarSymbol.Value, argEvaluated))
                    {
                        runtimeContext.RaiseRuntimeError(lastVarSymbol, RuntimeErrorType.UnknownSymbolMeaning, "Symbol '" + lastVarSymbol.Value + "' is not a defined variable in the current scope");
                    }
                    expectSymbol = true;
                }
            }
            yield break;
        }
    }
}

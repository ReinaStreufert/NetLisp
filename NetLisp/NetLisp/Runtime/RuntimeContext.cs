using NetLisp.Data;
using NetLisp.Text;

namespace NetLisp.Runtime
{
    public delegate void RuntimeErrorEvent(RuntimeError err);
    public delegate void SyntaxErrorEvent(SyntaxError err);

    public class RuntimeContext
    {
        public ScopeStack Scopes { get; set; } = new ScopeStack();
        public CallStack Calls { get; set; } = new CallStack();

        public event RuntimeErrorEvent RuntimeError;
        public event SyntaxErrorEvent SyntaxError;

        public RuntimeContext()
        {
            RuntimePrimitives.DefineCoreRoutines(Scopes.GlobalScope);
        }

        public IEnumerable<LispToken> EvaluateExpressions(string expr, string sourceName = "")
        {
            List<LispToken> parsedExpr = new List<LispToken>();
            LispListParser parser = new LispListParser(expr, sourceName);
            // read expressions until end of input
            TokenParseResult lastParseStatus = TokenParseResult.Success;
            while (lastParseStatus != TokenParseResult.EndOfInput)
            {
                // read one expression
                lastParseStatus = TokenParseResult.Success;
                while (lastParseStatus == TokenParseResult.Success)
                {
                    lastParseStatus = parser.ParseNext();
                }
                // determine if the expression broke due to natural conclusion or due to error
                if (lastParseStatus == TokenParseResult.EndOfExpression)
                {
                    parsedExpr.Add(parser.ParseResult);
                    continue; // next expression
                } else if (lastParseStatus == TokenParseResult.SyntaxError)
                {
                    SyntaxError?.Invoke(parser.LastError);
                    return null;
                }
            }
            if (parser.ParseResult != null)
            {
                parsedExpr.Add(parser.ParseResult);
            }
            return EvaluateExpressions(parsedExpr);
        }

        public IEnumerable<LispToken> EvaluateExpressions(IEnumerable<LispToken> expr)
        {
            List<LispToken> exprResults = new List<LispToken>();
            try
            {
                foreach (LispToken token in expr)
                {
                    if (token.Quoted)
                    {
                        token.Quoted = false;
                        exprResults.Add(token);
                    } else
                    {
                        IEnumerable<LispToken> result = token.Evaluate(this);
                        foreach (LispToken evaluatedToken in result)
                        {
                            exprResults.Add(evaluatedToken);
                        }
                    }
                }
            } catch (LispRuntimeException e)
            {
                resetStacks();
                RuntimeError?.Invoke(e.LispError);
                return null;
            }
            return exprResults;
        }

        public void RaiseRuntimeError(LispToken problemToken, RuntimeErrorType errType, string errMessage)
        {
            RuntimeError runtimeError = new RuntimeError();
            runtimeError.ErrorLocation = problemToken.SourceLocation;
            runtimeError.ErrorType = errType;
            runtimeError.Text = errMessage;
            runtimeError.Calls = Calls.Copy();
            runtimeError.Scopes = Scopes.Copy();
            throw new LispRuntimeException(runtimeError);
        }

        private void resetStacks()
        {
            Calls = new CallStack();
            Scopes = new ScopeStack(Scopes.GlobalScope);
        }
    }
}
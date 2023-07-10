using NetLisp.Data;
using NetLisp.Structs;
using NetLisp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    public class RuntimeError
    {
        public SourceReference ErrorLocation { get; set; }
        public RuntimeErrorType ErrorType { get; set; }
        public CallStack Calls { get; set; }
        public ScopeStack Scopes { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Runtime error:" + " " + Text);
            SourceReference nextCallLocation = ErrorLocation;
            foreach (CallTrace call in Calls.AllCallers().Reverse())
            {
                sb.Append("    at ");
                // attempt to find a name for the caller by searching the scope stack
                string? callerName = Scopes.CurrentScope.Search(call.CalledToken);
                if (callerName == null)
                {
                    sb.Append("<anonymous> ");
                } else
                {
                    sb.Append(callerName + " ");
                }
                sb.AppendLine(nextCallLocation.ToString());
                nextCallLocation = call.CallerLocation;
            }
            sb.AppendLine("    at <evaluationstart> " + nextCallLocation.ToString());
            return sb.ToString();
        }
    }
    public enum RuntimeErrorType
    {
        ArgumentMismatchError,
        UnknownSymbolMeaning,
        ExpectedSingleValue,
        SymbolAlreadyDefined
    }
    class LispRuntimeException : Exception
    {
        public RuntimeError LispError { get; set; }
        public override string Message => LispError.ToString();
        public LispRuntimeException(RuntimeError lispError)
        {
            LispError = lispError;
        }
    }
}

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
            sb.AppendLine("Runtime error at " + ErrorLocation.ToString() + ": " + Text);
            foreach (ExecutableLispToken caller in Calls.AllCallers().Reverse())
            {
                sb.Append("    at ");
                // attempt to find a name for the caller by searching the scope stack
                string? callerName = Scopes.CurrentScope.Search(caller);
                if (callerName == null)
                {
                    sb.AppendLine("<anonymous function>");
                } else
                {
                    sb.AppendLine(callerName);
                }
            }
            return sb.ToString();
        }
    }
    public enum RuntimeErrorType
    {

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

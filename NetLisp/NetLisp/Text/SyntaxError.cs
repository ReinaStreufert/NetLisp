using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    public class SyntaxError
    {
        public SourceReference ErrorLocation { get; set; }
        public SyntaxErrorType ErrorType { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return "Syntax error at " + ErrorLocation.ToString() + ": " + Text;
        }
    }
    public enum SyntaxErrorType
    {
        UnknownToken,
        WrongNumberOfCloseParens,
        UnexpectedEndOfInput,
        NotAList,
        ExpectedEndOfInput,
        UnrecognizedEscapeSequence
    }
}

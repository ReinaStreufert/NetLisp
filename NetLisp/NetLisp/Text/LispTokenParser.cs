using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    partial class LispListParser
    {
        private abstract class LispTokenParser
        {
            public abstract bool TryOpenToken(char input);
            public abstract bool TryContinueToken(char input);
            public abstract bool CloseToken(string token, LispListParser parser);
        }
        private class SymbolParser : LispTokenParser
        {
            private bool couldBeNumber = false;
            public override bool TryOpenToken(char input)
            {
                if (input == '-')
                {
                    couldBeNumber = true;
                }
                return RegularExpressions.SymbolFirstAllowedCharacters.IsMatch(input.ToString());
            }
            public override bool TryContinueToken(char input)
            {
                return RegularExpressions.SymbolAllowedCharacters.IsMatch(input.ToString());
            }
            public override bool CloseToken(string token, LispListParser parser)
            {
                if (couldBeNumber)
                {
                    float numberVal;
                    if (float.TryParse(token, out numberVal))
                    {
                        return parser.appendCurrentList(new LispNumber(numberVal));
                    }
                }
                return parser.appendCurrentList(new LispSymbol(token));
            }
        }
        private class NumberParser : LispTokenParser
        {
            public override bool TryOpenToken(char input)
            {
                return RegularExpressions.NumberAllowedCharacters.IsMatch(input.ToString());
            }
            public override bool TryContinueToken(char input)
            {
                return RegularExpressions.NumberAllowedCharacters.IsMatch(input.ToString());
            }
            public override bool CloseToken(string token, LispListParser parser)
            {
                float numberVal;
                if (float.TryParse(token, out numberVal))
                {
                    return parser.appendCurrentList(new LispNumber(numberVal));
                } else
                {
                    parser.LastError = new SyntaxError();
                    parser.LastError.ErrorType = SyntaxErrorType.UnknownToken;
                    parser.LastError.ErrorLocation = parser.lastTokenLocation;
                    parser.LastError.Text = "Unexpected token '" + token + "'";
                    return false;
                }
            }
        }
        private class TreeControlParser : LispTokenParser
        {
            public override bool TryOpenToken(char input)
            {
                return RegularExpressions.TreeControlCharacters.IsMatch(input.ToString());
            }
            public override bool TryContinueToken(char input)
            {
                return false;
            }
            public override bool CloseToken(string token, LispListParser parser)
            {
                if (token == "(")
                {
                    parser.pushNewList();
                    return true;
                } else if (token == ")")
                {
                    return parser.popList();
                } else
                {
                    // ?????
                    parser.LastError = new SyntaxError();
                    parser.LastError.ErrorType = SyntaxErrorType.UnknownToken;
                    parser.LastError.ErrorLocation = parser.lastTokenLocation;
                    parser.LastError.Text = "Unexpected token '" + token + "'";
                    return false;
                }
            }
        }
        private class WhitespaceParser : LispTokenParser
        {
            public override bool TryOpenToken(char input)
            {
                return RegularExpressions.WhitespaceCharacters.IsMatch(input.ToString());
            }
            public override bool TryContinueToken(char input)
            {
                return RegularExpressions.WhitespaceCharacters.IsMatch(input.ToString());
            }
            public override bool CloseToken(string token, LispListParser parser)
            {
                for (int i = 0; i < token.Length; i++)
                {
                    if (token[i] == '\n')
                    {
                        parser.newlineCount++;
                        parser.lastNewlinePosition = parser.lastTokenLocation.Position + i;
                    }
                }
                return true;
            }
        }
    }
}

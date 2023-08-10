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
            public abstract bool TryOpenToken(char input, LispListParser parser);
            public abstract bool TryContinueToken(char input, LispListParser parser);
            public abstract bool CloseToken(string token, LispListParser parser);
        }
        private class SymbolParser : LispTokenParser
        {
            private bool couldBeNumber = false;
            public override bool TryOpenToken(char input, LispListParser parser)
            {
                if (input == '-')
                {
                    couldBeNumber = true;
                }
                if (RegularExpressions.SymbolFirstAllowedCharacters.IsMatch(input.ToString()))
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Symbol);
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool TryContinueToken(char input, LispListParser parser)
            {
                if (RegularExpressions.SymbolAllowedCharacters.IsMatch(input.ToString()))
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Symbol);
                    return true;
                } else
                {
                    return false;
                }
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
        private class CommentParser : LispTokenParser
        {
            public override bool TryOpenToken(char input, LispListParser parser)
            {
                if (input == ';')
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Comment);
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool TryContinueToken(char input, LispListParser parser)
            {
                if (input != '\n')
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Comment);
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool CloseToken(string token, LispListParser parser)
            {
                return true;
            }
        }
        private class StringParser : LispTokenParser
        {
            private struct EscapeSequenceInfo
            {
                public char Identifier;
                public char Value;
            }
            private bool gotEndQuote = false;
            private bool gotEscape = false;
            private EscapeSequenceInfo[] escapeCodes = new[]
            {
                new EscapeSequenceInfo() { Identifier = '\"', Value = '\"' },
                new EscapeSequenceInfo() { Identifier = '\\', Value = '\\' },
                new EscapeSequenceInfo() { Identifier = 'n', Value = '\n' },
                new EscapeSequenceInfo() { Identifier = 'r', Value = '\r' },
                new EscapeSequenceInfo() { Identifier = 't', Value = '\t' },
                new EscapeSequenceInfo() { Identifier = 'b', Value = '\b' }
            };

            public override bool TryOpenToken(char input, LispListParser parser)
            {
                if (input == '"')
                {
                    gotEscape = false;
                    gotEndQuote = false;
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.String);
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool TryContinueToken(char input, LispListParser parser)
            {
                if (gotEndQuote)
                {
                    return false;
                } else
                {
                    bool hadEscape = gotEscape;
                    if (input == '"')
                    {
                        if (gotEscape)
                        {
                            gotEscape = false;
                        } else
                        {
                            gotEndQuote = true;
                        }
                    } else if (input == '\\')
                    {
                        if (gotEscape)
                        {
                            gotEscape = false;
                        } else
                        {
                            gotEscape = true;
                        }
                    } else if (gotEscape)
                    {
                        gotEscape = false;
                    }
                    if (hadEscape || gotEscape)
                    {
                        parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.StringEscaped);
                    }
                    else
                    {
                        parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.String);
                    }
                    return true;
                }
            }
            public override bool CloseToken(string token, LispListParser parser)
            {
                if (token.Length < 2 || token[token.Length - 1] != '"')
                {
                    parser.LastError = new SyntaxError();
                    parser.LastError.ErrorType = SyntaxErrorType.UnexpectedEndOfInput;
                    parser.LastError.ErrorLocation = parser.lastTokenLocation;
                    parser.LastError.Text = "Expected end quote";
                    return false;
                }
                string stringVal;
                if (token.Length > 2)
                {
                    StringBuilder escapedString = new StringBuilder();
                    gotEscape = false;
                    for (int i = 1; i < token.Length - 1; i++)
                    {
                        char currentChar = token[i];
                        if (gotEscape)
                        {
                            foreach (EscapeSequenceInfo escapeCode in escapeCodes)
                            {
                                if (currentChar == escapeCode.Identifier)
                                {
                                    escapedString.Append(escapeCode.Value);
                                    gotEscape = false;
                                    break;
                                }
                            }
                            if (gotEscape)
                            {
                                parser.LastError = new SyntaxError();
                                parser.LastError.ErrorType = SyntaxErrorType.UnrecognizedEscapeSequence;
                                parser.LastError.ErrorLocation = parser.lastTokenLocation;
                                parser.LastError.Text = "Unrecognized escape character '" + currentChar + "'";
                                return false;
                            }
                        } else
                        {
                            if (currentChar == '\\')
                            {
                                gotEscape = true;
                            } else
                            {
                                escapedString.Append(currentChar);
                            }
                        }
                    }
                    stringVal = escapedString.ToString();
                } else
                {
                    stringVal = "";
                }
                return parser.appendCurrentList(new LispString(stringVal));
            }
        }
        private class NumberParser : LispTokenParser
        {
            public override bool TryOpenToken(char input, LispListParser parser)
            {
                if (RegularExpressions.NumberAllowedCharacters.IsMatch(input.ToString()))
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Number);
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool TryContinueToken(char input, LispListParser parser)
            {
                if (RegularExpressions.NumberAllowedCharacters.IsMatch(input.ToString()))
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Number);
                    return true;
                } else
                {
                    return false;
                }
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
            public override bool TryOpenToken(char input, LispListParser parser)
            {
                if (RegularExpressions.TreeControlCharacters.IsMatch(input.ToString()))
                {
                    if (input == '(')
                    {
                        parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.OpenParen, true);
                    } else if (input == ')')
                    {
                        parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.CloseParen, true);
                    }
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool TryContinueToken(char input, LispListParser parser)
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
            public override bool TryOpenToken(char input, LispListParser parser)
            {
                if (RegularExpressions.WhitespaceCharacters.IsMatch(input.ToString()))
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Whitespace);
                    return true;
                } else
                {
                    return false;
                }
            }
            public override bool TryContinueToken(char input, LispListParser parser)
            {
                if (RegularExpressions.WhitespaceCharacters.IsMatch(input.ToString()))
                {
                    parser.OutputCharacterMap?.Write(input, CharacterMapTokenClass.Whitespace);
                    return true;
                }
                else
                {
                    return false;
                }
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

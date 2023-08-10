using NetLisp.Data;
using NetLisp.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    partial class LispListParser
    {
        private string inputText;
        private string sourceName;
        private Stack<LispList> listStack = new Stack<LispList>();
        private LispList currentList
        {
            get
            {
                return listStack.Peek();
            }
        }
        private List<LispTokenParser> parsers = new List<LispTokenParser>();
        private int newlineCount = 0;
        private int lastNewlinePosition = 0;
        private bool quoteFlagSet = false;
        private SourceReference lastTokenLocation;

        public CharacterMap<CharacterMapTokenClass> OutputCharacterMap { get; set; } = null;
        public int Position { get; private set; } = 0;
        public LispList ParseResult { get; private set; } = null;
        public SyntaxError LastError { get; private set; } = null;

        public LispListParser(string inputText, string sourceName = "", int startPosition = 0, bool createCharacterMap = false)
        {
            this.inputText = inputText;
            this.sourceName = sourceName;
            this.Position = startPosition;
            parsers.Add(new SymbolParser());
            parsers.Add(new CommentParser());
            parsers.Add(new StringParser());
            parsers.Add(new NumberParser());
            parsers.Add(new TreeControlParser());
            parsers.Add(new WhitespaceParser());
            if (createCharacterMap)
            {
                OutputCharacterMap = new CharacterMap<CharacterMapTokenClass>();
            }
        }

        public TokenParseResult ParseNext()
        {
            if (IsEndOfInput())
            {
                // if ParseNext is called not for the first time and it is the end of input, it indicates
                // misuse of the LispListParser class rather than a syntax error. If it is the first call,
                // it is because the input is empty which is a syntax issue rather than code misuse.
                if (Position == 0)
                {
                    LastError = new SyntaxError();
                    LastError.ErrorType = SyntaxErrorType.NotAList;
                    LastError.ErrorLocation = GetLocation();
                    LastError.Text = "No input to parse";
                    return TokenParseResult.SyntaxError;
                } else
                {
                    throw new InvalidOperationException("Cannot continue parsing after end of input");
                }
            }
            lastTokenLocation = GetLocation();
            if (listStack.Count == 0)
            {
                // start parsing a new expression
                ParseResult = null;
            }
            StringBuilder tokenText = new StringBuilder();
            char tokenFirstChar = nextChar();
            if (tokenFirstChar == '\'')
            {
                if (quoteFlagSet)
                {
                    LastError = new SyntaxError();
                    LastError.ErrorType = SyntaxErrorType.UnexpectedEndOfInput;
                    LastError.ErrorLocation = lastTokenLocation;
                    LastError.Text = "Expected value after quote, got quote";
                    return TokenParseResult.SyntaxError;
                }
                quoteFlagSet = true;
                if (IsEndOfInput())
                {
                    LastError = new SyntaxError();
                    LastError.ErrorType = SyntaxErrorType.UnexpectedEndOfInput;
                    LastError.ErrorLocation = lastTokenLocation;
                    LastError.Text = "Expected value after quote, got end of input";
                    return TokenParseResult.SyntaxError;
                }
                OutputCharacterMap?.Write(tokenFirstChar, CharacterMapTokenClass.Quote);
                lastTokenLocation = GetLocation();
                tokenFirstChar = nextChar();
            }
            LispTokenParser usedParser = null;
            foreach (LispTokenParser checkParser in parsers)
            {
                if (checkParser.TryOpenToken(tokenFirstChar, this))
                {
                    usedParser = checkParser;
                    break;
                }
            }
            if (usedParser == null)
            {
                LastError = new SyntaxError();
                LastError.ErrorType = SyntaxErrorType.UnknownToken;
                LastError.ErrorLocation = lastTokenLocation;
                LastError.Text = "Unexpected token '" + tokenFirstChar + "'";
                return TokenParseResult.SyntaxError;
            }
            tokenText.Append(tokenFirstChar);
            for (; ; )
            {
                if (IsEndOfInput())
                {
                    if (usedParser.CloseToken(tokenText.ToString(), this))
                    {
                        return resolveEndOfInput();
                    } else
                    {
                        return TokenParseResult.SyntaxError;
                    }
                }
                char inputChar = nextChar();
                if (usedParser.TryContinueToken(inputChar, this))
                {
                    tokenText.Append(inputChar);
                } else
                {
                    if (usedParser.CloseToken(tokenText.ToString(), this))
                    {
                        Position--;
                        if (IsEndOfInput())
                        {
                            return resolveEndOfInput();
                        }
                        if (ParseResult != null)
                        {
                            return TokenParseResult.EndOfExpression;
                        } else
                        {
                            return TokenParseResult.Success;
                        }
                    } else
                    {
                        Position--;
                        return TokenParseResult.SyntaxError;
                    }
                }
            }
        }

        private TokenParseResult resolveEndOfInput()
        {
            if (quoteFlagSet)
            {
                LastError = new SyntaxError();
                LastError.ErrorType = SyntaxErrorType.UnexpectedEndOfInput;
                LastError.ErrorLocation = lastTokenLocation;
                LastError.Text = "Expected value after quote, got end of input";
                ParseResult = listStack.Last();
                return TokenParseResult.SyntaxError;
            }
            if (listStack.Count > 0)
            {
                LastError = new SyntaxError();
                LastError.ErrorType = SyntaxErrorType.WrongNumberOfCloseParens;
                LastError.ErrorLocation = lastTokenLocation;
                LastError.Text = "Expected " + listStack.Count + " more closing parens";
                ParseResult = listStack.Last();
                return TokenParseResult.SyntaxError;
            }
            return TokenParseResult.EndOfInput;
        }

        private bool appendCurrentList(LispToken token)
        {
            if (listStack.Count == 0)
            {
                LastError = new SyntaxError();
                LastError.ErrorType = SyntaxErrorType.NotAList;
                LastError.ErrorLocation = lastTokenLocation;
                LastError.Text = "Expression does not start with opening paren and is not a list";
                return false;
            }
            if (quoteFlagSet)
            {
                token.Quoted = true;
                quoteFlagSet = false;
            }
            token.SourceLocation = lastTokenLocation;
            currentList.Items.Add(token);
            return true;
        }
        private void pushNewList()
        {
            LispList newList = new LispList();
            if (quoteFlagSet)
            {
                newList.Quoted = true;
                quoteFlagSet = false;
            }
            newList.SourceLocation = lastTokenLocation;
            if (listStack.Count > 0)
            {
                currentList.Items.Add(newList);
            }
            listStack.Push(newList);
        }
        private bool popList()
        {
            if (listStack.Count == 0)
            {
                LastError = new SyntaxError();
                LastError.ErrorType = SyntaxErrorType.WrongNumberOfCloseParens;
                LastError.ErrorLocation = lastTokenLocation;
                LastError.Text = "More closing parens than opening parens";
                return false;
            }
            if (quoteFlagSet)
            {
                LastError = new SyntaxError();
                LastError.ErrorType = SyntaxErrorType.UnexpectedEndOfInput;
                LastError.ErrorLocation = lastTokenLocation;
                LastError.Text = "Expected value after quote, got end of list";
                quoteFlagSet = false; // for error-ignore parsing
                return false;
            }
            LispList popResult = listStack.Pop();
            if (listStack.Count == 0)
            {
                ParseResult = popResult;
            }
            return true;
        }

        public SourceReference GetLocation()
        {
            return new SourceReference(sourceName, newlineCount, Position - lastNewlinePosition, Position);
        }
        public bool IsEndOfInput()
        {
            return Position >= inputText.Length;
        }
        private char nextChar()
        {
            char val = inputText[Position];
            Position++;
            return val;
        }
    }
    enum TokenParseResult
    {
        Success,
        SyntaxError,
        EndOfExpression,
        EndOfInput
    }
    public enum CharacterMapTokenClass
    {
        Symbol,
        Number,
        String,
        StringEscaped,
        OpenParen,
        CloseParen,
        Comment,
        Quote,
        Whitespace
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    static class RegularExpressions
    {
        // used for symbol name validation in LispSymbol
        public static Regex SymbolMatchExpression { get; private set; } = new Regex("[^\\d\\s.();\"][^\\s;\"]*");

        // used to direct input character-by-character during parsing
        public static Regex NumberAllowedCharacters { get; private set; } = new Regex("[\\d.]");
        public static Regex SymbolFirstAllowedCharacters { get; private set; } = new Regex("[^\\d\\s.();\"]"); // - is only included so that tokens starting in - are initially handed to number and only get treated as a symbol if that fails
        public static Regex SymbolAllowedCharacters { get; private set; } = new Regex("[^\\s();\"]");
        public static Regex TreeControlCharacters { get; private set; } = new Regex("[()]");
        public static Regex WhitespaceCharacters { get; private set; } = new Regex("\\s");

        public static bool EnsureEntireMatch(Regex expression, string input)
        {
            Match match = expression.Match(input);
            if (!match.Success)
            {
                return false;
            }
            return (match.Length == input.Length);
        }
    }
}

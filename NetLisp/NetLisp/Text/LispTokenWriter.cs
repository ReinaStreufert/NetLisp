using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text
{
    static class LispTokenWriter
    {
        public static string ListToString(List<LispToken> items)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('(');
            for (int i = 0; i < items.Count; i++)
            {
                sb.Append(items[i].ToString());
                if (i < items.Count - 1)
                {
                    sb.Append(' ');
                }
            }
            sb.Append(')');
            return sb.ToString();
        }
        // All other LispToken.ToString implementations are one-liners and as such are kept in
        // NetLisp.Data instead of NetLisp.Text
        public const string FunctionToString = ".func.";
        public const string MacroToString = ".macro.";
        public const string SpecialFormToString = ".specialform.";
    }
}

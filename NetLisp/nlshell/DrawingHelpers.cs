using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlshell
{
    static class DrawingHelpers
    {
        public static string[] WrapText(string text, int maxWidth)
        {
            List<string> lines = new List<string>();
            StringBuilder currentLine = new StringBuilder();
            string[] words = text.Split(' ');
            foreach (string word in words)
            {
                if (currentLine.Length == 0)
                {
                    currentLine.Append(word);
                } else
                {
                    if (currentLine.Length + word.Length + 1 <= maxWidth)
                    {
                        currentLine.Append(" ");
                        currentLine.Append(word);
                    } else
                    {
                        lines.Add(currentLine.ToString());
                        currentLine = new StringBuilder();
                        currentLine.Append(word);
                    }
                }
            }
            lines.Add(currentLine.ToString());
            return lines.ToArray();
        }
    }
}

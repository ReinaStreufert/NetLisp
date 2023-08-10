using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Structs
{
    public struct SourceReference
    {
        public string SourceName { get; set; } = "";
        public int Line { get; set; } // stored zero-based
        public int Column { get; set; } // stored zero-based
        public int Position { get; set; } // raw zero-based character position in the file including \n and \r chars
        public SourceReference(string sourceName, int line, int column, int position)
        {
            SourceName = sourceName;
            Line = line;
            Column = column;
            Position = position;
        }
        public override string ToString()
        {
            if (SourceName == "")
            {
                return "(" + (Line + 1).ToString() + "," + (Column + 1) + ")";
            } else
            {
                return "(" + SourceName + " " + (Line + 1).ToString() + "," + (Column + 1) + ")";
            }
        }
    }
}

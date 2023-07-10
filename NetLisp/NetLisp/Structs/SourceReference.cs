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
        public int Line { get; set; }
        public int Column { get; set; }
        public int Position { get; set; }
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
                return "(" + Line.ToString() + "," + Column + ")";
            } else
            {
                return "(" + SourceName + " " + Line.ToString() + "," + Column + ")";
            }
        }
    }
}

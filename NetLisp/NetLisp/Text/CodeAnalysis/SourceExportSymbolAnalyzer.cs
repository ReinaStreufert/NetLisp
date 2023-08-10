using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Text.CodeAnalysis
{
    // provides a way to simulate require and load expressions so their symbols can be used during source
    // analysis when a source is edited to reference another.
    // not abstract, just not implemented
    public class SourceExportSymbolAnalyzer
    {
        public ExportSymbol[] GetOrLoadModule(string moduleName)
        {
            return new ExportSymbol[0];
        }
        public ExportSymbol[] GetOrLoadFile(string fileName)
        {
            return new ExportSymbol[0];
        }
    }
    public class ExportSymbol
    {
        public string SymbolName { get; set; }
        public ExportType ExportScope { get; set; }
        public LispToken Value { get; set; }
    }
    public enum ExportType
    {
        GlobalDefine,
        ReturnToken // symbol name will be blank for this type
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetLisp.Runtime;
using NetLisp.Structs;

namespace NetLisp.Data
{
    public abstract class LispToken
    {
        public abstract LispDataType Type { get; }
        public abstract bool TypeRequiresEvaluation { get; }
        public abstract bool TypeCanBeExecuted { get; }
        public abstract IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext);

        public bool Quoted { get; set; }
        public SourceReference SourceLocation { get; set; }
    }
    public enum LispDataType
    {
        List,
        Symbol,
        Number,
        Boolean,
        Function,
        Macro,
        SpecialForm,
        DotNetObject
    }
}

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
        public abstract bool TypeCanBeExecuted { get; }
        public abstract IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext);
        public abstract bool CompareValue(LispToken token); // used by = operator
        public abstract int HashValue(); // used by table extended type and is generally useful for lots of implementations
        public virtual bool IsImmediateReturnToken { get => false; }

        public bool Quoted { get; set; }
        public SourceReference SourceLocation { get; set; }

        public LispTypeInfo GetTypeInfo()
        {
            return LispTypeInfo.FromInstance(this);
        }

        // GetHashCode is kept intact so reference-based hashing can still occur if needed
    }
    public abstract class SpecialLispToken : LispToken // special tokens are used for internal-signaling in the runtime about specific special cases
    {
        public abstract LispSpecialType SpecialType { get; }
        public override LispDataType Type => LispDataType.SpecialToken;
        public override bool TypeCanBeExecuted => false;

        public override bool CompareValue(LispToken token)
        {
            return false; // not a valid or useful operation
        }
        public override int HashValue()
        {
            return 0; // even less valid and less useful operation
        }
    }
    public abstract class ExtendedLispToken : LispToken
    {
        public sealed override LispDataType Type => LispDataType.ExtendedType;
        public sealed override bool TypeCanBeExecuted => false;

        public abstract ExtendedTypeInfo ExtendedTypeInfo { get; }

        public override IEnumerable<LispToken> Evaluate(RuntimeContext runtimeContext)
        {
            yield return this;
        }

        public override string ToString()
        {
            return "." + ExtendedTypeInfo.ExtendedTypeName + ".";
        }
    }

    public enum LispDataType
    {
        List,
        Symbol,
        Number,
        Boolean,
        String,
        Function,
        Macro,
        SpecialForm,
        SpecialToken,
        ExtendedType
    }
    public enum LispSpecialType
    {
        ReturnTuple,
        ImmediateReturn
    }
}

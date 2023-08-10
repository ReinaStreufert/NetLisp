using NetLisp.Data;
using NetLisp.Runtime.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime
{
    static class RuntimePrimitives
    {
        public static void DefineCoreRoutines(Scope globalScope)
        {
            globalScope.Define("+", new PlusOperator());
            globalScope.Define("-", new MinusOperator());
            globalScope.Define("*", new TimesOperator());
            globalScope.Define("/", new DivideOperator());
            globalScope.Define("define", new Define());
            globalScope.Define("setq", new Setq());
            globalScope.Define("lambda", new Lambda());
            globalScope.Define("macro", new Macro());
            globalScope.Define("=", new Equals());
            globalScope.Define(">", new GreaterThan());
            globalScope.Define("<", new LessThan());
            globalScope.Define("not", new Not());
            globalScope.Define("or", new Or());
            globalScope.Define("and", new And());
            globalScope.Define("if", FlowNativeMacros.CreateIfMacro(globalScope));
            globalScope.Define("values", new Values());
            globalScope.Define("runitback", new Runitback());
            globalScope.Define("returnvalues", new ReturnValues());
            globalScope.Define("let", new Let());
            globalScope.Define("return-from", new ReturnFrom());
            globalScope.Define("quote", new Quote());
            globalScope.Define("require", new Require());
            globalScope.Define("load", new Load());
            globalScope.Define("typestr", new TypeStr());
            globalScope.Define("printstr", new PrintStr());

            globalScope.Define("true", new LispBoolean(true));
            globalScope.Define("false", new LispBoolean(false));
        }
    }
}

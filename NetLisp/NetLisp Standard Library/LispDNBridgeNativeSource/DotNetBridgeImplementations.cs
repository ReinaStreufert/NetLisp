using NetLisp.Data;
using NetLisp.Runtime;

namespace LispDNBridgeNativeSource
{
    public class DotNetBridgeImplementations : INativeSource
    {
        public LispToken OnSourceLoad(RuntimeContext runtimeContext)
        {
            BridgeReflectionResources.Initialize();
            Scope global = runtimeContext.Scopes.GlobalScope;

            global.Define("dnuse", new LispMacro(new NativeExecutableBody(DnUseNativeMacro), ScopeStack.ConstructFromScope(global), null, new LispSymbol("namespaceList"), new LispSymbol("body")));
            global.Define("dnunpack", new LispMacro(new NativeExecutableBody(DnUnpackNativeMacro), ScopeStack.ConstructFromScope(global), null, new LispSymbol("namespaceList")));
            global.Define("dnuset", new LispMacro(new NativeExecutableBody(DnUseTNativeMacro), ScopeStack.ConstructFromScope(global), null, new LispSymbol("typeExpr"), new LispSymbol("baseName"), new LispSymbol("body")));
            global.Define("dnunpackt", new LispMacro(new NativeExecutableBody(DnUnpackTNativeMacro), ScopeStack.ConstructFromScope(global), null, new LispSymbol("typeExpr"), new LispSymbol("baseName")));
            global.Define("dngenerict", new GenericT());
            global.Define("dngenericm", new GenericM());
            global.Define("dnexactreturn", new ExactReturn());
            global.Define("dnbridgecast", new BridgeCast());
            global.Define("dnloadasm", new LoadAsm());
            global.Define("dnftype", new LispFunction(new NativeExecutableBody(DnFType), ScopeStack.ConstructFromScope(global), null, new LispSymbol("typeName")));
            global.Define("dnenuminst", new EnumInst());

            global.Define("dnobjtypestr", new LispString(LispTypeInfo.FromExtendedTypeInfo(DotnetInstance.DotnetInstanceExtendedTypeInfo).TypeStr));
            global.Define("dntypetypestr", new LispString(LispTypeInfo.FromExtendedTypeInfo(DotnetType.DotnetTypeExtendedTypeInfo).TypeStr));
            return new LispList();
        }

        public IEnumerable<LispToken> DnUseNativeMacro(RuntimeContext runtimeContext)
        {
            string[] namespaces = readNamespaceList(runtimeContext);
            LispList body = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("body"), LispDataType.List);

            LispList definitionList = new LispList();
            IEnumerable<BridgeSymbol> bridgeSymbols = BridgeReflectionResources.GenerateBridgeSymbols(namespaces);
            foreach (BridgeSymbol bridgeSymbol in bridgeSymbols)
            {
                definitionList.Items.Add(new LispSymbol(bridgeSymbol.SymbolName));
                definitionList.Items.Add(bridgeSymbol.CreateBridgeDefinition());
            }

            LispList letStatement = new LispList();
            letStatement.Items.Add(new LispSymbol("let"));
            letStatement.Items.Add(definitionList);
            letStatement.Items.Add(body);
            yield return letStatement;
        }

        public IEnumerable<LispToken> DnUnpackNativeMacro(RuntimeContext runtimeContext)
        {
            string[] namespaces = readNamespaceList(runtimeContext);

            LispList defineStatement = new LispList();
            defineStatement.Items.Add(new LispSymbol("define"));
            IEnumerable<BridgeSymbol> bridgeSymbols = BridgeReflectionResources.GenerateBridgeSymbols(namespaces);
            foreach (BridgeSymbol bridgeSymbol in bridgeSymbols)
            {
                defineStatement.Items.Add(new LispSymbol(bridgeSymbol.SymbolName));
                defineStatement.Items.Add(bridgeSymbol.CreateBridgeDefinition());
            }

            yield return defineStatement;
        }

        public IEnumerable<LispToken> DnUseTNativeMacro(RuntimeContext runtimeContext)
        {
            LispToken typeExpr = runtimeContext.Scopes.CurrentScope.Get("typeExpr");
            LispSymbol baseName = runtimeContext.Assert<LispSymbol>(runtimeContext.Scopes.CurrentScope.Get("baseName"), LispDataType.Symbol);
            LispList body = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("body"), LispDataType.List);

            LispList definitionList = new LispList();
            bool firstResult = true;
            foreach (LispToken evaluationResult in typeExpr.Evaluate(runtimeContext))
            {
                if (firstResult)
                {
                    firstResult = false;
                } else
                {
                    runtimeContext.RaiseRuntimeError(typeExpr, RuntimeErrorType.ExpectedSingleValue, "Type expression evaluated to multiple values");
                }
                Type type = runtimeContext.Assert<DotnetType>(evaluationResult, DotnetType.DotnetTypeExtendedTypeInfo).TypeInstance;
                foreach (BridgeSymbol bridgeSymbol in BridgeReflectionResources.GenerateBridgeSymbols(type, baseName.Value))
                {
                    definitionList.Items.Add(new LispSymbol(bridgeSymbol.SymbolName));
                    definitionList.Items.Add(bridgeSymbol.CreateBridgeDefinition());
                }
            }

            LispList letStatement = new LispList();
            letStatement.Items.Add(new LispSymbol("let"));
            letStatement.Items.Add(definitionList);
            letStatement.Items.Add(body);
            yield return letStatement;
        }

        public IEnumerable<LispToken> DnUnpackTNativeMacro(RuntimeContext runtimeContext)
        {
            LispToken typeExpr = runtimeContext.Scopes.CurrentScope.Get("typeExpr");
            LispSymbol baseName = runtimeContext.Assert<LispSymbol>(runtimeContext.Scopes.CurrentScope.Get("baseName"), LispDataType.Symbol);

            LispList defineStatement = new LispList();
            defineStatement.Items.Add(new LispSymbol("define"));
            bool firstResult = true;
            foreach (LispToken evaluationResult in typeExpr.Evaluate(runtimeContext))
            {
                if (firstResult)
                {
                    firstResult = false;
                }
                else
                {
                    runtimeContext.RaiseRuntimeError(typeExpr, RuntimeErrorType.ExpectedSingleValue, "Type expression evaluated to multiple values");
                }
                Type type = runtimeContext.Assert<DotnetType>(evaluationResult, DotnetType.DotnetTypeExtendedTypeInfo).TypeInstance;
                foreach (BridgeSymbol bridgeSymbol in BridgeReflectionResources.GenerateBridgeSymbols(type, baseName.Value))
                {
                    defineStatement.Items.Add(new LispSymbol(bridgeSymbol.SymbolName));
                    defineStatement.Items.Add(bridgeSymbol.CreateBridgeDefinition());
                }
            }
            yield return defineStatement;
        }

        public IEnumerable<LispToken> DnFType(RuntimeContext runtimeContext)
        {
            LispString typeName = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("typeName"), LispDataType.String);
            Type? type = Type.GetType(typeName.Value);
            if (type == null)
            {
                runtimeContext.RaiseRuntimeError(typeName, RuntimeErrorType.Other, "the given dotnet type name was not found");
            }
            yield return new DotnetType(type);
        }

        private static string[] readNamespaceList(RuntimeContext runtimeContext)
        {
            LispList namespaceList = runtimeContext.Assert<LispList>(runtimeContext.Scopes.CurrentScope.Get("namespaceList"), LispDataType.List);

            string[] namespaces = new string[namespaceList.Items.Count];
            for (int i = 0; i < namespaceList.Items.Count; i++)
            {
                LispSymbol namespaceSymbol = runtimeContext.Assert<LispSymbol>(namespaceList.Items[i], LispDataType.Symbol);
                namespaces[i] = namespaceSymbol.Value;
            }
            return namespaces;
        }
    }
}
using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static NetLisp.Text.CharacterMap<NetLisp.Text.CharacterMapTokenClass>;

namespace NetLisp.Text.CodeAnalysis
{
    public class SourceAnalyzer
    {
        private Scope baseGlobalScope;
        private SourceExportSymbolAnalyzer exportSymbolSource;
        private ScopeAnalysisKeyword[] scopeSpecialAnalyzers = new ScopeAnalysisKeyword[]
        {
            new DefineKeyword(),
            new LetKeyword(),
            new SetqKeyword(),
            new LambdaKeyword(),
            new aLambdaKeyword(),
            new MacroKeyword(),
            new RequireKeyword(),
            new LoadKeyword(),//,
            new DnUseKeyword()
          //new TbuseKeyword(),
          //new DnUseKeyword
        };

        public SourceAnalyzer(SourceExportSymbolAnalyzer exportSymbolAnalyzer, Scope baseGlobalScope = null)
        {
            exportSymbolSource = exportSymbolAnalyzer;
            if (baseGlobalScope == null)
            {
                this.baseGlobalScope = Scope.CreateGlobal();
            }
            else
            {
                this.baseGlobalScope = baseGlobalScope;
            }
        }

        public SourceAnalysis Analyze(string source)
        {
            LispListParser parser = new LispListParser(source, "", 0, true);
            List<LispList> expressions = new List<LispList>();
            while (!parser.IsEndOfInput())
            {
                TokenParseResult status = parser.ParseNext();
                if (status == TokenParseResult.EndOfExpression)
                {
                    expressions.Add(parser.ParseResult);
                }
            }
            if (parser.ParseResult != null)
            {
                expressions.Add(parser.ParseResult);
            }
            ScopeStack globalScopeStack = new ScopeStack(baseGlobalScope);
            ScopeStack analysisScopeStack = new ScopeStack();
            List<ListScopeAnalysis> expressionsAnalyzed = new List<ListScopeAnalysis>();
            foreach (LispList list in expressions)
            {
                expressionsAnalyzed.Add(analyzeScope(null, list, analysisScopeStack, globalScopeStack, parser.OutputCharacterMap));
            }
            SourceAnalysis analysis = new SourceAnalysis();
            analysis.ExpressionScopeData = expressionsAnalyzed;
            analysis.CharacterMap = parser.OutputCharacterMap;
            return analysis;
        }

        private LispToken? attemptRequire(LispSymbol assocSymbol, ScopeStack globalScopeStack)
        {
            return import(exportSymbolSource.GetOrLoadModule(assocSymbol.Value), globalScopeStack);
        }
        private LispToken? attemptLoad(string fileName, ScopeStack globalScopeStack)
        {
            return import(exportSymbolSource.GetOrLoadFile(fileName), globalScopeStack);
        }

        private LispToken? import(ExportSymbol[] exportSymbols, ScopeStack globalScopeStack)
        {
            LispToken? moduleReturn = null;
            bool globalStackPushed = false;
            foreach (ExportSymbol exportSymbol in exportSymbols)
            {
                if (exportSymbol.ExportScope == ExportType.GlobalDefine)
                {
                    if (!globalStackPushed)
                    {
                        globalScopeStack.Push();
                        globalStackPushed = true;
                    }
                    globalScopeStack.CurrentScope.Define(exportSymbol.SymbolName, exportSymbol.Value);
                }
                else if (exportSymbol.ExportScope == ExportType.ReturnToken)
                {
                    moduleReturn = exportSymbol.Value;
                }
            }
            return moduleReturn;
        }

        private ListScopeAnalysis analyzeScope(ListScopeAnalysis parent, LispList list, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
        {
            ListScopeAnalysis listAnalysis = new ListScopeAnalysis();
            listAnalysis.SourceList = list;
            listAnalysis.ParentList = parent;
            listAnalysis.OpenParenPosition = list.SourceLocation.Position;
            listAnalysis.CloseParenPosition = findCloseParen(listAnalysis.OpenParenPosition, characterMap);
            listAnalysis.InnerBuiltScope = analysisScopeStack.CurrentScope;
            listAnalysis.InnerGlobalScope = globalScopeStack.CurrentScope;

            ScopeAnalysisKeyword keyword = null;
            if (list.Items.Count > 0 && list.Items[0].Type == LispDataType.Symbol)
            {
                LispSymbol executionSymbol = (LispSymbol)list.Items[0];
                foreach (ScopeAnalysisKeyword checkKeyword in scopeSpecialAnalyzers)
                {
                    if (checkKeyword.Keyword == executionSymbol.Value)
                    {
                        keyword = checkKeyword;
                    }
                }
            }
            listAnalysis.ChildLists = new List<ListScopeAnalysis>();
            if (keyword == null)
            {
                foreach (LispToken token in list.Items)
                {
                    if (token.Type == LispDataType.List)
                    {
                        listAnalysis.ChildLists.Add(analyzeScope(listAnalysis, (LispList)token, analysisScopeStack, globalScopeStack, characterMap));
                    }
                }
            }
            else
            {
                foreach (ListScopeAnalysis childAnalysis in keyword.AnalyzeChildScopes(this, listAnalysis, analysisScopeStack, globalScopeStack, characterMap))
                {
                    listAnalysis.ChildLists.Add(childAnalysis);
                }
            }

            return listAnalysis;
        }

        private int findCloseParen(int openParen, CharacterMap<CharacterMapTokenClass> characterMap)
        {
            CharacterRange range = characterMap[openParen];
            if (range.Classification != CharacterMapTokenClass.OpenParen)
            {
                throw new ArgumentException("openParen position was not an open paren");
            }
            int startIndex = characterMap.Ranges.IndexOf(range) + 1;
            int parenLevel = 1;
            for (int i = startIndex; i < characterMap.Ranges.Count; i++)
            {
                CharacterRange checkRange = characterMap.Ranges[i];
                if (checkRange.Classification == CharacterMapTokenClass.OpenParen)
                {
                    parenLevel++;
                }
                else if (checkRange.Classification == CharacterMapTokenClass.CloseParen)
                {
                    parenLevel--;
                    if (parenLevel <= 0)
                    {
                        return checkRange.Start;
                    }
                }
            }
            return -1;
        }

        private abstract class ScopeAnalysisKeyword
        {
            public abstract string Keyword { get; }
            public abstract IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap);
        }

        private abstract class DefinitionKeyword : ScopeAnalysisKeyword
        {
            private abstract class AssignmentValueKeyword
            {
                public abstract string Keyword { get; }
                public abstract LispToken? GetValue(SourceAnalyzer analyzer, ScopeStack globalScopeStack, LispList expr);
            }
            private class RequireAssignmentKeyword : AssignmentValueKeyword
            {
                public override string Keyword => "require";
                public override LispToken? GetValue(SourceAnalyzer analyzer, ScopeStack globalScopeStack, LispList expr)
                {
                    if (expr.Items.Count == 2 && expr.Items[1].Type == LispDataType.Symbol)
                    {
                        return analyzer.attemptRequire((LispSymbol)expr.Items[1], globalScopeStack);
                    } else
                    {
                        return new AnalysisToken(MetadataTypeRestrictions.NoType);
                    }
                }
            }
            private class LoadAssignmentKeyword : AssignmentValueKeyword
            {
                public override string Keyword => "load";
                public override LispToken? GetValue(SourceAnalyzer analyzer, ScopeStack globalScopeStack, LispList expr)
                {
                    if (expr.Items.Count == 2 && expr.Items[1].Type == LispDataType.String)
                    {
                        return analyzer.attemptLoad(((LispString)expr.Items[1]).Value, globalScopeStack);
                    }
                    else
                    {
                        return new AnalysisToken(MetadataTypeRestrictions.NoType);
                    }
                }
            }
            private abstract class FunctionAssignmentKeyword : AssignmentValueKeyword
            {
                public override LispToken? GetValue(SourceAnalyzer analyzer, ScopeStack globalScopeStack, LispList expr)
                {
                    if (expr.Items.Count > 1 && expr.Items[1].Type == LispDataType.List)
                    {
                        LispList argList = (LispList)expr.Items[1];
                        for (int i = 0; i < argList.Items.Count; i++)
                        {
                            if (argList.Items[i].Type != LispDataType.Symbol)
                            {
                                return new AnalysisToken(new LispFunction(null, null, null));
                            }
                        }
                        LispSymbol[] args = argList.Items.Cast<LispSymbol>().ToArray();
                        if (expr.Items.Count > 3 && expr.Items[2].Type == LispDataType.List)
                        {
                            LispList metadataBody = (LispList)expr.Items[2];
                            ArgumentDefinedMetadata metadata;
                            if (!ArgumentDefinedMetadata.TryParse(args, metadataBody, out metadata))
                            {
                                return new AnalysisToken(new LispFunction(null, null, null, args));
                            }
                            return new AnalysisToken(new LispFunction(null, null, metadata, args));
                        } else
                        {
                            return new AnalysisToken(new LispFunction(null, null, null, args));
                        }
                    } else
                    {
                        return new AnalysisToken(new LispFunction(null, null, null));
                    }
                }
            }
            private class LambdaAssignmentKeyword : FunctionAssignmentKeyword
            {
                public override string Keyword => "lambda";
            }
            private class aLambdaAssignmentKeyword : FunctionAssignmentKeyword
            {
                public override string Keyword => "alambda";
            }
            static List<AssignmentValueKeyword> keywords = new List<AssignmentValueKeyword>()
            {
                new RequireAssignmentKeyword(),
                new LoadAssignmentKeyword(),
                new LambdaAssignmentKeyword(),
                new aLambdaAssignmentKeyword()
            };

            protected LispToken analyzeAssignmentValue(LispToken value, SourceAnalyzer analyzer, ScopeStack analysisScopeStack, ScopeStack globalScopeStack)
            {
                if (value.Quoted)
                {
                    return new AnalysisToken(value);
                }
                if (value.Type == LispDataType.List)
                {
                    LispList expr = (LispList)value;
                    if (expr.Items.Count == 0)
                    {
                        return new AnalysisToken(MetadataTypeRestrictions.AnyType);
                    }
                    if (expr.Items[0].Type == LispDataType.Symbol)
                    {
                        string keyword = ((LispSymbol)expr.Items[0]).Value;
                        foreach (AssignmentValueKeyword keywordHandler in keywords)
                        {
                            if (keywordHandler.Keyword == keyword)
                            {
                                return keywordHandler.GetValue(analyzer, globalScopeStack, expr);
                            }
                        }
                        LispToken? keywordVal = analysisScopeStack.CurrentScope.Get(keyword);
                        if (keywordVal == null)
                        {
                            keywordVal = globalScopeStack.CurrentScope.Get(keyword);
                            if (keywordVal == null)
                            {
                                return new AnalysisToken(MetadataTypeRestrictions.AnyType);
                            }
                        }
                        if (keywordVal.TypeCanBeExecuted)
                        {
                            ExecutableTokenMetadata metadata = ((ExecutableLispToken)keywordVal).Metadata;
                            return new AnalysisToken(metadata.ReturnParam.TypeRestrictions);
                        } else if (keywordVal.GetTypeInfo() == AnalysisToken.AnalysisTokenExtendedTypeInfo)
                        {
                            AnalysisToken keywordAnalysisToken = (AnalysisToken)keywordVal;
                            if (keywordAnalysisToken.PotentialExecutableDefinition != null)
                            {
                                return new AnalysisToken(keywordAnalysisToken.PotentialExecutableDefinition.Metadata.ReturnParam.TypeRestrictions);
                            } else
                            {
                                return new AnalysisToken(MetadataTypeRestrictions.AnyType);
                            }
                        } else
                        {
                            return new AnalysisToken(MetadataTypeRestrictions.AnyType);
                        }
                    }
                    else
                    {
                        return new AnalysisToken(value);
                    }
                }
                else if (value.Type == LispDataType.Symbol)
                { 
                    LispSymbol symb = (LispSymbol)value;
                    LispToken? symbolEval = analysisScopeStack.CurrentScope.Get(symb.Value);
                    if (symbolEval == null)
                    {
                        symbolEval = globalScopeStack.CurrentScope.Get(symb.Value);
                    }
                    return new AnalysisToken(symbolEval);
                }
                else
                {
                    return new AnalysisToken(value);
                }
            }
        }

        private class SetqKeyword : DefinitionKeyword
        {
            public override string Keyword => "setq";

            public override IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
            {
                LispList setqStatement = parent.SourceList;
                for (int i = 1; i < setqStatement.Items.Count; i += 2)
                {
                    if (setqStatement.Items[i].Type == LispDataType.Symbol && i + 1 < setqStatement.Items.Count)
                    {
                        LispSymbol assignSymbol = (LispSymbol)setqStatement.Items[i];
                        LispToken? assignValue = analyzeAssignmentValue(setqStatement.Items[i + 1], analyzer, analysisScopeStack, globalScopeStack);
                        LispToken? currentSymbolValue = analysisScopeStack.CurrentScope.Get(assignSymbol.Value);
                        if (currentSymbolValue == null)
                        {
                            currentSymbolValue = globalScopeStack.CurrentScope.Get(assignSymbol.Value);
                        }
                        if (currentSymbolValue != null)
                        {
                            if (currentSymbolValue.GetTypeInfo() == AnalysisToken.AnalysisTokenExtendedTypeInfo)
                            {
                                AnalysisToken analysisToken = (AnalysisToken)currentSymbolValue;
                                analysisToken.MergeAssign(assignValue);
                            }
                        } else
                        {
                            if (!analysisScopeStack.CurrentScope.Set(assignSymbol.Value, assignValue))
                            {
                                globalScopeStack.CurrentScope.Set(assignSymbol.Value, assignValue);
                            }
                        }
                        if (setqStatement.Items[i + 1].Type == LispDataType.List)
                        {
                            yield return analyzer.analyzeScope(parent, (LispList)(setqStatement.Items[i + 1]), analysisScopeStack, globalScopeStack, characterMap);
                        }
                    }
                }
            }
        }

        private class DefineKeyword : DefinitionKeyword
        {
            public override string Keyword => "define";

            public override IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
            {
                parent.InnerWritingStyle = LispWritingStyle.SExpressionDefinitionList;
                globalScopeStack.Push();
                parent.InnerGlobalScope = globalScopeStack.CurrentScope;
                LispList defineStatement = parent.SourceList;
                for (int i = 1; i < defineStatement.Items.Count; i += 2)
                {
                    if (defineStatement.Items[i].Type == LispDataType.Symbol)
                    {
                        LispSymbol defSymbol = (LispSymbol)defineStatement.Items[i];
                        globalScopeStack.CurrentScope.Define(defSymbol.Value);
                        if (i + 1 < defineStatement.Items.Count)
                        {
                            LispToken valueToken = (LispToken)defineStatement.Items[i + 1];
                            globalScopeStack.CurrentScope.Set(defSymbol.Value, analyzeAssignmentValue(valueToken, analyzer, analysisScopeStack, globalScopeStack));
                            if (valueToken.Type == LispDataType.List)
                            {
                                LispList valueList = (LispList)valueToken;
                                yield return analyzer.analyzeScope(parent, valueList, analysisScopeStack, globalScopeStack, characterMap);
                            }
                        }
                    }
                }
            }
        }

        private class LetKeyword : DefinitionKeyword
        {
            public override string Keyword => "let";

            public override IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
            {
                parent.InnerWritingStyle = LispWritingStyle.SpecialSExpression;
                LispList letStatement = parent.SourceList;
                if (letStatement.Items.Count > 1 && letStatement.Items[1].Type == LispDataType.List)
                {
                    LispList definitionList = (LispList)letStatement.Items[1];
                    ListScopeAnalysis definitionListAnalysis = new ListScopeAnalysis();
                    analysisScopeStack.Push();
                    definitionListAnalysis.ParentList = parent;
                    definitionListAnalysis.SourceList = definitionList;
                    definitionListAnalysis.InnerWritingStyle = LispWritingStyle.IsolatedDefinitionList;
                    definitionListAnalysis.InnerGlobalScope = globalScopeStack.CurrentScope;
                    definitionListAnalysis.InnerBuiltScope = analysisScopeStack.CurrentScope;
                    definitionListAnalysis.OpenParenPosition = definitionList.SourceLocation.Position;
                    definitionListAnalysis.CloseParenPosition = analyzer.findCloseParen(definitionList.SourceLocation.Position, characterMap);
                    definitionListAnalysis.ChildLists = new List<ListScopeAnalysis>();
                    for (int i = 0; i < definitionList.Items.Count; i += 2)
                    {
                        if (definitionList.Items[i].Type == LispDataType.Symbol)
                        {
                            LispSymbol defSymbol = (LispSymbol)definitionList.Items[i];
                            analysisScopeStack.CurrentScope.Define(defSymbol.Value);
                            if (i + 1 < definitionList.Items.Count)
                            {
                                LispToken valueToken = (LispToken)definitionList.Items[i + 1];
                                analysisScopeStack.CurrentScope.Set(defSymbol.Value, analyzeAssignmentValue(valueToken, analyzer, analysisScopeStack, globalScopeStack));
                                if (valueToken.Type == LispDataType.List)
                                {
                                    LispList valueList = (LispList)valueToken;
                                    definitionListAnalysis.ChildLists.Add(analyzer.analyzeScope(definitionListAnalysis, valueList, analysisScopeStack, globalScopeStack, characterMap));
                                }
                            }
                        }
                    }
                    yield return definitionListAnalysis;
                    if (letStatement.Items.Count > 2 && letStatement.Items[2].Type == LispDataType.List)
                    {
                        ListScopeAnalysis bodyAnalysis = analyzer.analyzeScope(parent, (LispList)letStatement.Items[2], analysisScopeStack, globalScopeStack, characterMap);
                        bodyAnalysis.InnerWritingStyle = LispWritingStyle.ExecutableBody;
                        yield return bodyAnalysis;
                    }
                    analysisScopeStack.Pop();
                }
            }
        }

        private abstract class FunctionKeyword : ScopeAnalysisKeyword
        {
            public override IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
            {
                parent.InnerWritingStyle = LispWritingStyle.SpecialSExpression;
                LispList functionDec = parent.SourceList;
                if (functionDec.Items.Count > 1 && functionDec.Items[1].Type == LispDataType.List)
                {
                    LispList argList = (LispList)functionDec.Items[1];
                    ListScopeAnalysis argListAnalysis = new ListScopeAnalysis();
                    analysisScopeStack.Push();
                    argListAnalysis.ParentList = parent;
                    argListAnalysis.SourceList = argList;
                    argListAnalysis.InnerWritingStyle = LispWritingStyle.ArgumentList;
                    argListAnalysis.InnerGlobalScope = globalScopeStack.CurrentScope;
                    argListAnalysis.InnerBuiltScope = analysisScopeStack.CurrentScope;
                    argListAnalysis.OpenParenPosition = argList.SourceLocation.Position;
                    argListAnalysis.CloseParenPosition = analyzer.findCloseParen(argList.SourceLocation.Position, characterMap);
                    argListAnalysis.ChildLists = new List<ListScopeAnalysis>();
                    List<LispSymbol> args = new List<LispSymbol>();
                    for (int i = 0; i < argList.Items.Count; i++)
                    {
                        if (argList.Items[i].Type == LispDataType.Symbol)
                        {
                            LispSymbol argSymbol = (LispSymbol)argList.Items[i];
                            analysisScopeStack.CurrentScope.Define(argSymbol.Value);
                            args.Add(argSymbol);
                        }
                    }
                    yield return argListAnalysis;
                    if (functionDec.Items.Count > 2 && functionDec.Items[2].Type == LispDataType.List)
                    {
                        if (functionDec.Items.Count > 3 && functionDec.Items[3].Type == LispDataType.List)
                        {
                            LispList metadataBody = (LispList)functionDec.Items[2];
                            if (args.Count > 0)
                            {
                                ArgumentDefinedMetadata metadata;
                                if (ArgumentDefinedMetadata.TryParse(args.ToArray(), metadataBody, out metadata))
                                {
                                    foreach (ArgumentMetadataEntry argumentMetadata in metadata.Entries.OfType<ArgumentMetadataEntry>())
                                    {
                                        analysisScopeStack.CurrentScope.Set(argumentMetadata.Param.Value, new AnalysisToken(argumentMetadata.TypeRestrictions));
                                    }
                                }
                            }
                            ListScopeAnalysis metadataAnalysis = analyzer.analyzeScope(parent, metadataBody, analysisScopeStack, globalScopeStack, characterMap);
                            metadataAnalysis.InnerWritingStyle = LispWritingStyle.MetadataBody;
                            foreach (ListScopeAnalysis metadataChild in metadataAnalysis.ChildLists)
                            {
                                metadataChild.InnerWritingStyle = LispWritingStyle.MetadataEntry;
                            }
                            yield return metadataAnalysis;
                            ListScopeAnalysis bodyAnalysis = analyzer.analyzeScope(parent, (LispList)functionDec.Items[3], analysisScopeStack, globalScopeStack, characterMap);
                            bodyAnalysis.InnerWritingStyle = LispWritingStyle.ExecutableBody;
                            yield return bodyAnalysis;
                        }
                        else
                        {
                            ListScopeAnalysis bodyAnalysis = analyzer.analyzeScope(parent, (LispList)functionDec.Items[2], analysisScopeStack, globalScopeStack, characterMap);
                            bodyAnalysis.InnerWritingStyle = LispWritingStyle.ExecutableBody;
                            yield return bodyAnalysis;
                        }
                    }
                    analysisScopeStack.Pop();
                }
            }
        }
        private class LambdaKeyword : FunctionKeyword
        {
            public override string Keyword => "lambda";
        }
        private class aLambdaKeyword : FunctionKeyword
        {
            public override string Keyword => "alambda";
        }
        private class MacroKeyword : FunctionKeyword
        {
            public override string Keyword => "macro";
        }
        private class DnUseKeyword : FunctionKeyword // to be replaced with a real analyzer for dnuse
        {
            public override string Keyword => "dnuse";
        }
        private class RequireKeyword : ScopeAnalysisKeyword
        {
            public override string Keyword => "require";

            public override IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
            {
                parent.InnerWritingStyle = LispWritingStyle.SpecialSExpression;
                LispList requireStatement = parent.SourceList;
                if (requireStatement.Items.Count > 1 && requireStatement.Items[1].Type == LispDataType.Symbol)
                {
                    LispSymbol reqSymbol = (LispSymbol)requireStatement.Items[1];
                    analyzer.attemptRequire(reqSymbol, globalScopeStack);
                }
                foreach (LispToken token in requireStatement.Items)
                {
                    if (token.Type == LispDataType.List)
                    {
                        yield return analyzer.analyzeScope(parent, (LispList)token, analysisScopeStack, globalScopeStack, characterMap);
                    }
                }
            }
        }
        private class LoadKeyword : ScopeAnalysisKeyword
        {
            public override string Keyword => "load";

            public override IEnumerable<ListScopeAnalysis> AnalyzeChildScopes(SourceAnalyzer analyzer, ListScopeAnalysis parent, ScopeStack analysisScopeStack, ScopeStack globalScopeStack, CharacterMap<CharacterMapTokenClass> characterMap)
            {
                parent.InnerWritingStyle = LispWritingStyle.SpecialSExpression;
                LispList loadStatement = parent.SourceList;
                if (loadStatement.Items.Count > 1 && loadStatement.Items[1].Type == LispDataType.String)
                {
                    LispString fileName = (LispString)loadStatement.Items[1];
                    analyzer.attemptLoad(fileName.Value, globalScopeStack);
                }
                foreach (LispToken token in loadStatement.Items)
                {
                    if (token.Type == LispDataType.List)
                    {
                        yield return analyzer.analyzeScope(parent, (LispList)token, analysisScopeStack, globalScopeStack, characterMap);
                    }
                }
            }
        }
    }
    public class ListScopeAnalysis
    {
        public LispList SourceList { get; set; }
        public int OpenParenPosition { get; set; }
        public int CloseParenPosition { get; set; }
        public Scope InnerBuiltScope { get; set; }
        public Scope InnerGlobalScope { get; set; }
        public LispWritingStyle InnerWritingStyle { get; set; } = LispWritingStyle.SExpression;
        public ListScopeAnalysis ParentList { get; set; }
        public List<ListScopeAnalysis> ChildLists { get; set; }
    }
    public class SourceAnalysis
    {
        public CharacterMap<CharacterMapTokenClass> CharacterMap { get; set; }
        public List<ListScopeAnalysis> ExpressionScopeData { get; set; }
        public ListScopeAnalysis? SearchExpressionScopeData(int position)
        {
            foreach (ListScopeAnalysis expressionData in ExpressionScopeData)
            {
                if (position > expressionData.OpenParenPosition && position <= expressionData.CloseParenPosition)
                {
                    return searchAnalysisRecursive(expressionData, position);
                }
            }
            return null;
        }
        private static ListScopeAnalysis searchAnalysisRecursive(ListScopeAnalysis analysis, int position)
        {
            foreach (ListScopeAnalysis child in analysis.ChildLists)
            {
                if (position > child.OpenParenPosition && position <= child.CloseParenPosition)
                {
                    return searchAnalysisRecursive(child, position);
                }
            }
            return analysis;
        }
    }
    public enum LispWritingStyle
    {
        SExpression, // normal writing style
        SExpressionDefinitionList, // such as define
        IsolatedDefinitionList, // such as the definition part of a let statement
        ArgumentList,
        SpecialSExpression,
        ExecutableBody,
        MetadataBody,
        MetadataEntry
    }
}

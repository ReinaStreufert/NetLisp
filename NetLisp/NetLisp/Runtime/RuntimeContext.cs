using NetLisp.Data;
using NetLisp.Structs;
using NetLisp.Text;

namespace NetLisp.Runtime
{
    public delegate void RuntimeErrorEvent(RuntimeError err);
    public delegate void SyntaxErrorEvent(SyntaxError err);

    public class RuntimeContext
    {
        public const string Version = "1.0.0";

        public ScopeStack Scopes { get; set; } = new ScopeStack();
        public CallStack Calls { get; set; } = new CallStack();
        public string ModuleSearchDirectory { get; set; } = "modules"; // behavior of module search may be changed by overriding FindModuleConfigPath
        public SandboxingFlags SandboxOptions { get; set; }

        public event RuntimeErrorEvent RuntimeError;
        public event SyntaxErrorEvent SyntaxError;

        protected Dictionary<string, LispToken> moduleStore = new Dictionary<string, LispToken>();
        private bool currentlyEvaluating = false;

        public RuntimeContext(SandboxingFlags sandboxOptions = SandboxingFlags.None)
        {
            RuntimePrimitives.DefineCoreRoutines(Scopes.GlobalScope);
            SandboxOptions = sandboxOptions;
        }

        public IEnumerable<LispToken> EvaluateExpressions(string expr, string sourceName = "", EvaluationFlags flags = EvaluationFlags.None) // returns null (and raises event) in case of syntax error
        {
            List<LispToken> parsedExpr = new List<LispToken>();
            LispListParser parser = new LispListParser(expr, sourceName);
            // read expressions until end of input
            TokenParseResult lastParseStatus = TokenParseResult.Success;
            while (lastParseStatus != TokenParseResult.EndOfInput)
            {
                // read one expression
                lastParseStatus = TokenParseResult.Success;
                while (lastParseStatus == TokenParseResult.Success)
                {
                    lastParseStatus = parser.ParseNext();
                }
                // determine if the expression broke due to natural conclusion or due to error
                if (lastParseStatus == TokenParseResult.EndOfExpression)
                {
                    parsedExpr.Add(parser.ParseResult);
                    continue; // next expression
                } else if (lastParseStatus == TokenParseResult.SyntaxError)
                {
                    SyntaxError?.Invoke(parser.LastError);
                    return null;
                }
            }
            if (parser.ParseResult != null)
            {
                parsedExpr.Add(parser.ParseResult);
            }
            return EvaluateExpressions(parsedExpr, flags);
        }

        public IEnumerable<LispToken> EvaluateExpressions(IEnumerable<LispToken> expr, EvaluationFlags flags = EvaluationFlags.None)
        {
            if (!currentlyEvaluating)
            {
                currentlyEvaluating = true;
                IEnumerable<LispToken> result;
                try
                {
                    result = evaluateExpressionsCore(expr, flags);
                }
                catch (LispRuntimeException e)
                {
                    recoverFromAbort();
                    RuntimeError?.Invoke(e.LispError);
                    return null;
                } catch (Exception e) // feels dirty but since netlisp code can do .net stuff literally any exception could be thrown in the runtime and should be caught as a lisp error
                {
                    RuntimeError dotNetError = new RuntimeError();
                    dotNetError.ErrorType = RuntimeErrorType.DotNetException;
                    dotNetError.ErrorLocation = Calls.CurrentlyEvaluatingToken.SourceLocation;
                    dotNetError.Calls = Calls.Copy();
                    dotNetError.Scopes = Scopes.Copy();
                    dotNetError.Text = e.ToString();
                    recoverFromAbort();
                    RuntimeError?.Invoke(dotNetError);
                    return null;
                }
                currentlyEvaluating = false;
                return result;
            } else
            {
                return evaluateExpressionsCore(expr, flags);
            }
        }
        private IEnumerable<LispToken> evaluateExpressionsCore(IEnumerable<LispToken> expr, EvaluationFlags flags)
        {
            List<LispToken> exprResults = new List<LispToken>();
            foreach (LispToken token in expr)
            {
                if (token.Quoted)
                {
                    token.Quoted = false;
                    exprResults.Add(token);
                }
                else
                {
                    IEnumerable<LispToken> result = token.Evaluate(this);
                    foreach (LispToken evaluatedToken in result)
                    {
                        if (evaluatedToken.Type == LispDataType.SpecialToken)
                        {
                            Calls.CurrentlyEvaluatingToken = evaluatedToken;
                            foreach (LispToken evaluatedSpecialToken in evaluatedToken.Evaluate(this))
                            {
                                exprResults.Add(evaluatedSpecialToken);
                            }
                        }
                        else
                        {
                            exprResults.Add(evaluatedToken);
                        }
                    }
                }
            }
            if (flags.HasFlag(EvaluationFlags.ReturnAllResults))
            {
                return exprResults;
            } else
            {
                if (exprResults.Count > 0)
                {
                    return exprResults.GetRange(exprResults.Count - 1, 1);
                } else
                {
                    return exprResults;
                }
            }
        }

        protected internal virtual bool ScreenArbitraryFileLoad(string requestedFilePath)
        {
            return true;
        }

        protected virtual string? FindModuleConfigPath(LispSymbol assocSymbol)
        {
            if (!Directory.Exists(ModuleSearchDirectory))
            {
                return null;
            }
            string[] subDirs = Directory.GetDirectories(ModuleSearchDirectory);
            foreach (string subDir in subDirs)
            {
                if (Path.GetFileName(subDir) == assocSymbol.Value)
                {
                    string configPath = subDir + Path.DirectorySeparatorChar + "nlispmodule.json";
                    if (File.Exists(configPath))
                    {
                        return configPath;
                    } else
                    {
                        return null;
                    }
                }
            }
            return null;
        }

        public ModuleLoadResult LoadModule(LispSymbol assocSymbol)
        {
            if (moduleStore.ContainsKey(assocSymbol.Value))
            {
                return new ModuleLoadResult(ModuleLoadStatus.Success, null, moduleStore[assocSymbol.Value]);
            }
            string? configPath = FindModuleConfigPath(assocSymbol);
            if (configPath == null)
            {
                return new ModuleLoadResult(ModuleLoadStatus.ModuleConfigNotFound);
            }
            string moduleConfigText = File.ReadAllText(configPath);
            ModuleConfig moduleConfig;
            string symbolName = assocSymbol.Value;
            if (!ModuleConfigParser.TryParseModuleConfig(moduleConfigText, out moduleConfig))
            {
                return new ModuleLoadResult(ModuleLoadStatus.ModuleConfigInvalid);
            }
            if (moduleConfig.SymbolName != symbolName || moduleConfig.SourceChain.Count < 1)
            {
                return new ModuleLoadResult(ModuleLoadStatus.ModuleConfigInvalid);
            }
            List<SourceInfo> sourceChain = moduleConfig.SourceChain;
            string moduleBaseDirectory = Directory.GetParent(configPath).FullName;
            LispToken lastSourceResult = null;
            for (int i = 0; i < sourceChain.Count; i++)
            {
                SourceInfo source = sourceChain[i];
                string sourceFullPath = Path.GetFullPath(source.Path, moduleBaseDirectory);
                if (!File.Exists(sourceFullPath))
                {
                    return new ModuleLoadResult(ModuleLoadStatus.ModuleSourceNotFound, sourceFullPath);
                }
                if (source.IsNativeSource)
                {
                    string innerType = "LispModule.MainSource";
                    if (source.InnerType != null)
                    {
                        innerType = source.InnerType;
                    }
                    INativeSource nativeSource;
                    if (!INativeSource.TryLoadFromFile(sourceFullPath, innerType, out nativeSource))
                    {
                        return new ModuleLoadResult(ModuleLoadStatus.NativeSourceInvalid, sourceFullPath);
                    }
                    try
                    {
                        lastSourceResult = nativeSource.OnSourceLoad(this);
                    } catch (Exception ex)
                    {
                        return new ModuleLoadResult(ModuleLoadStatus.ModuleSourceError, sourceFullPath);
                    }
                    moduleStore[symbolName] = lastSourceResult;
                } else
                {
                    FileLoadResult fileLoadResult = reloadSourceFileCore(sourceFullPath, out lastSourceResult);
                    if (fileLoadResult == FileLoadResult.SourceError)
                    {
                        return new ModuleLoadResult(ModuleLoadStatus.ModuleSourceError, sourceFullPath);
                    }
                    moduleStore[symbolName] = lastSourceResult;
                }
            }
            return new ModuleLoadResult(ModuleLoadStatus.Success, null, lastSourceResult);
        }

        public FileLoadResult LoadSourceFile(string filename, out LispToken sourceResult)
        {
            filename = Path.GetFullPath(filename);
            if (moduleStore.ContainsKey(filename))
            {
                sourceResult = moduleStore[filename];
                return FileLoadResult.Success;
            } else
            {
                return reloadSourceFileCore(filename, out sourceResult);
            }
        }
        public FileLoadResult ReloadSourceFile(string filename, out LispToken sourceResult)
        {
            return reloadSourceFileCore(Path.GetFullPath(filename), out sourceResult);
        }
        private FileLoadResult reloadSourceFileCore(string filename, out LispToken sourceResult)
        {
            if (!File.Exists(filename))
            {
                sourceResult = null;
                return FileLoadResult.NotFound;
            }
            string fileText = File.ReadAllText(filename);
            ScopeStack savedScope = Scopes;
            Scopes = ScopeStack.ConstructFromScope(Scopes.GlobalScope);
            IEnumerable<LispToken> fileResult = EvaluateExpressions(fileText, filename);
            if (fileResult == null)
            {
                sourceResult = null;
                return FileLoadResult.SourceError;
            } else
            {
                // EvaluateExpressions returns an IEnumerable only because EvaluationFlags.ReturnAllValues
                // may be set. since we did not set it, we know it will be one value
                sourceResult = fileResult.First();
                Scopes = savedScope;
                moduleStore[filename] = sourceResult;
                return FileLoadResult.Success;
            }
        }
        public T Assert<T>(LispToken lispToken, LispDataType type) where T : LispToken
        {
            if (lispToken.Type == type)
            {
                return (T)lispToken;
            } else
            {
                RaiseRuntimeError(lispToken, RuntimeErrorType.ArgumentMismatchError, "Expected " + type.ToString().ToLower() + " got " + assertErrMessageTypeName(lispToken));
                return null; // happy compiler
            }
        }
        public T Assert<T>(LispToken lispToken, ExtendedTypeInfo type) where T : ExtendedLispToken
        {
            if (lispToken.Type == LispDataType.ExtendedType)
            {
                ExtendedLispToken extendedLispToken = (ExtendedLispToken)lispToken;
                if (extendedLispToken.ExtendedTypeInfo.ExtendedTypeGuid == type.ExtendedTypeGuid)
                {
                    return (T)extendedLispToken;
                }
            }
            RaiseRuntimeError(lispToken, RuntimeErrorType.ArgumentMismatchError, "Exepected " + assertErrMessageTypeName(type) + " got " + assertErrMessageTypeName(lispToken));
            return null; // happyyyyy compiler
        }
        private string assertErrMessageTypeName(LispToken lispToken)
        {
            if (lispToken.Type == LispDataType.ExtendedType)
            {
                ExtendedTypeInfo extInfo = ((ExtendedLispToken)lispToken).ExtendedTypeInfo;
                return assertErrMessageTypeName(extInfo);
            } else
            {
                return lispToken.Type.ToString().ToLower();
            }
        }
        private string assertErrMessageTypeName(ExtendedTypeInfo extInfo)
        {
            return extInfo.ExtendedTypeName + "-" + extInfo.ExtendedTypeGuid.ToString("N").Substring(0, 8);
        }

        public void RaiseRuntimeError(LispToken problemToken, RuntimeErrorType errType, string errMessage)
        {
            RuntimeError runtimeError = new RuntimeError();
            runtimeError.ErrorLocation = problemToken.SourceLocation;
            runtimeError.ErrorType = errType;
            runtimeError.Text = errMessage;
            runtimeError.Calls = Calls.Copy();
            runtimeError.Scopes = Scopes.Copy();
            throw new LispRuntimeException(runtimeError);
        }

        private void recoverFromAbort()
        {
            currentlyEvaluating = false;
            Calls = new CallStack();
            Scopes = new ScopeStack(Scopes.GlobalScope);
        }
    }
    [Flags]
    public enum SandboxingFlags
    {
        None = 0,
        AllowArbitraryFileLoad = 1,
        ForbidRequire = 2
    }
    [Flags]
    public enum EvaluationFlags
    {
        None = 0,
        ReturnAllResults
    }
    public enum FileLoadResult
    {
        Success,
        SourceError, // indicates syntax or runtime error in the file. appropriate event will be raised
        NotFound
    }
}
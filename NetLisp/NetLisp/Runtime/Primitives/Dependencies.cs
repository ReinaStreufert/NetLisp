using NetLisp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLisp.Runtime.Primitives
{
    class Require : LispSpecialForm
    {
        public override bool EvaluateArguments => false;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (runtimeContext.SandboxOptions.HasFlag(SandboxingFlags.ForbidRequire))
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.SandboxViolation, "Sandbox violation: 'require' is not permitted");
            }
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "'require' takes 1 argument");
            }
            LispSymbol assocSymbol = runtimeContext.Assert<LispSymbol>(passedArgs[0], LispDataType.Symbol);
            ModuleLoadResult loadResult = runtimeContext.LoadModule(assocSymbol);
            if (loadResult.Status == ModuleLoadStatus.Success)
            {
                yield return loadResult.LoadResult;
                yield break;
            } else
            {
                if (loadResult.Status == ModuleLoadStatus.ModuleConfigNotFound)
                {
                    runtimeContext.RaiseRuntimeError(assocSymbol, RuntimeErrorType.DependencyLoadError, "Module config for '" + assocSymbol.Value + "' could not be found. Check that it is installed");
                } else if (loadResult.Status == ModuleLoadStatus.ModuleConfigInvalid)
                {
                    runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.DependencyLoadError, "Module config for '" + assocSymbol.Value + "' is invalid");
                } else if (loadResult.Status == ModuleLoadStatus.ModuleSourceNotFound)
                {
                    runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.DependencyLoadError, "Module config for '" + assocSymbol.Value + "' points to non-existent source path '" + loadResult.RelevantSourcePath + "'");
                } else if (loadResult.Status == ModuleLoadStatus.NativeSourceInvalid)
                {
                    runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.DependencyLoadError, "Module config for '" + assocSymbol.Value + "' points to invalid native source path '" + loadResult.RelevantSourcePath + "'");
                } else if (loadResult.Status == ModuleLoadStatus.ModuleSourceError)
                {
                    if (Path.GetExtension(loadResult.RelevantSourcePath) == ".dll")
                    {
                        runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.DependencyLoadError, "Failed to load module '" + assocSymbol.Value + "' due to dotnet exception in native source '" + loadResult.RelevantSourcePath + "'");
                    } else
                    {
                        // if any type of runtime exception occurred, we would not be here. LispRuntimeException
                        // would have been thrown and handled well below where this method will ever end up on the
                        // call stack. therefore it is safe to assume all source errors are syntax errors
                        runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.DependencyLoadError, "Failed to load module '" + assocSymbol.Value + "' due to syntax error in source '" + loadResult.RelevantSourcePath + "'");
                    }
                }
            }
        }
    }
    class Load : LispSpecialForm
    {
        public override bool EvaluateArguments => true;

        protected override IEnumerable<LispToken> InnerExecute(List<LispToken> passedArgs, RuntimeContext runtimeContext, LispList entireTarget)
        {
            if (!runtimeContext.SandboxOptions.HasFlag(SandboxingFlags.AllowArbitraryFileLoad))
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.SandboxViolation, "Sandbox violation: 'load' is not permitted");
            }
            if (passedArgs.Count != 1)
            {
                runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.ArgumentMismatchError, "'load' takes 1 argument");
            }
            string fileNameString = (runtimeContext.Assert<LispString>(passedArgs[0], LispDataType.String)).Value;
            LispToken loadResult;
            FileLoadResult loadStatus = runtimeContext.LoadSourceFile(fileNameString, out loadResult);
            if (loadStatus == FileLoadResult.Success)
            {
                yield return loadResult;
            } else
            {
                if (loadStatus == FileLoadResult.NotFound)
                {
                    runtimeContext.RaiseRuntimeError(passedArgs[0], RuntimeErrorType.DependencyLoadError, "Specified file does not exist or is not available for access");
                } else if (loadStatus == FileLoadResult.SourceError)
                {
                    runtimeContext.RaiseRuntimeError(entireTarget, RuntimeErrorType.DependencyLoadError, "Syntax error in file");
                }
            }
        }
    }
}

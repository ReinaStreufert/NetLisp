using NetLisp.Data;
using NetLisp.Runtime;
using NetLisp.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlispcr
{
    static class HelpDisplay
    {
        const string helpText = @"nlispcr usage:
  -s  --source  : The path to the source file from which execution should start from
  -l  --libpath : Explicitly specify path to the directory which contains the standard library and installed modules
  -r  --result  : Prints the lisp token returned by the last line of the source file once execution is complete
  -v  --version : Displays version info for the current version of NetLisp and of nlispcr
  -?  --help    : Displays this usage guide

EX: nlipscr -s ""myfile.nlisp""
EX: nlipscr --libpath ""somewhere\other\than\usal"" --source ""myfile.nlisp""
EX: nlispcr -s ""myfile.nlisp"" --result
EX: nlispcr --version
EX: nlispcr -?";

        public static void Start()
        {
            Console.WriteLine(helpText);
        }
    }
    static class VersionDisplay
    {
        public const string Version = "1.0.0";

        public static void Start()
        {
            Console.WriteLine("nlispcr version: " + Version + " / NetLisp runtime version: " + RuntimeContext.Version);
        }
    }
    static class SourceExecution
    {
        public static void Start(NlispcrCommandLineParser commandLineArgs)
        {
            if (commandLineArgs.Source == null)
            {
                Console.WriteLine("No source path is specified");
                Environment.Exit(-1);
            }
            RuntimeContext runtimeContext = new RuntimeContext(SandboxingFlags.AllowArbitraryFileLoad);
            if (commandLineArgs.LibpathDir != null)
            {
                runtimeContext.ModuleSearchDirectory = commandLineArgs.LibpathDir;
            }
            runtimeContext.SyntaxError += (SyntaxError err) => Console.WriteLine(err.ToString());
            runtimeContext.RuntimeError += (RuntimeError err) => Console.WriteLine(err.ToString());
            LispToken sourceResult;
            FileLoadResult fileLoadResult = runtimeContext.LoadSourceFile(commandLineArgs.Source, out sourceResult);
            if (fileLoadResult == FileLoadResult.Success && commandLineArgs.Result)
            {
                Console.WriteLine("==>" + sourceResult.ToString());
            }
        }
    }
}

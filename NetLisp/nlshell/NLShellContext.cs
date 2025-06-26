using LispTableNativeSource;
using NetLisp.Data;
using NetLisp.Runtime;
using NetLisp.Text;
using NetLisp.Text.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace nlshell
{
    public class NLShellContext
    {
        public const string Version = "1.0.0";

        public RuntimeContext RuntimeContext { get; set; }

        private LispTable nlshellTable;
        private SourceAnalyzer sourceAnalyzer;
        private ConsoleLispEditor.ScriptHost scriptHost;
        private ConsoleLispEditor activeEditor;
        private List<string> pastSources = new List<string>();
        private int pastSourceInd = 0;

        private string openLast = null;
        private string saveLast = null;
        private int openLastPos = -1;

        public NLShellContext()
        {
            RuntimeContext = new RuntimeContext(SandboxingFlags.AllowArbitraryFileLoad);
            sourceAnalyzer = new SourceAnalyzer(new SourceExportSymbolAnalyzer(), RuntimeContext.Scopes.GlobalScope);
            nlshellTable = new NLShellLispTable(this);
            RuntimeContext.Scopes.GlobalScope.Define("nlshell", nlshellTable);
            RuntimeContext.Scopes.GlobalScope.Define("nlshell-setmodulepath",
                new LispFunction(new NativeExecutableBody(setModulePath),
                ScopeStack.ConstructFromScope(RuntimeContext.Scopes.GlobalScope),
                null,
                new LispSymbol("path")));
            RuntimeContext.SyntaxError += RuntimeContext_SyntaxError;
            RuntimeContext.RuntimeError += RuntimeContext_RuntimeError;
        }

        public void SetOpenBuffer(string text)
        {
            openLast = text;
        }
        public string GetSavedBuffer()
        {
            string oldSaveLast = saveLast;
            saveLast = null;
            return oldSaveLast;
        }

        private void RuntimeContext_RuntimeError(RuntimeError err)
        {
            Console.CursorLeft = 2;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("=> ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(err.ToString().TrimEnd('\n').Replace("\n", "\n     "));
            Console.Write("     ");
        }

        private void RuntimeContext_SyntaxError(NetLisp.Text.SyntaxError err)
        {
            Console.CursorLeft = 2;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("=> ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(err.ToString().TrimEnd('\n').Replace("\n", "\n     "));
            Console.Write("     ");
        }

        public void Start(string wakePath)
        {
            LispToken wakeOutput;
            RuntimeContext.LoadSourceFile(wakePath, out wakeOutput);
            scriptHost = new ConsoleLispEditor.ScriptHost(RuntimeContext.ModuleSearchDirectory);
            bool editorScriptsAdded = false;
            Random r = new Random();
            Console.CursorLeft = 2;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("=> ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Welcome to NLShell!");
            Console.WriteLine();
            while (true)
            {
                string consString = "cons" + r.Next(ushort.MaxValue).ToString();
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkMagenta;
                Console.CursorLeft = 3;
                Console.Write("[" + Environment.CurrentDirectory + " " + consString + "]");
                Console.BackgroundColor = ConsoleColor.Black;
                Console.WriteLine();
                int initialCursorPos = 1;
                activeEditor = new ConsoleLispEditor(sourceAnalyzer, 5, Console.CursorTop, "()");
                activeEditor.CreepUp = true;
                activeEditor.BottomMargin = 4;
                scriptHost.Editor = activeEditor;
                if (!editorScriptsAdded)
                {
                    foreach (string scriptFile in Directory.GetFiles("shellsources" + Path.DirectorySeparatorChar + "editor"))
                    {
                        scriptHost.AddScript(scriptFile);
                    }
                    editorScriptsAdded = true;
                }
                if (openLast != null)
                {
                    string startText = openLast;
                    if (openLastPos > -1)
                    {
                        initialCursorPos = openLastPos;
                    }
                    else
                    {
                        initialCursorPos = startText.Length;
                    }
                    activeEditor.SetText(startText);
                    openLast = null;
                    openLastPos = -1;
                }
                activeEditor.CursorPosition = initialCursorPos;
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.Enter, enterKeyBinding));
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.R, enterKeyBinding));
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.UpArrow, historyKeyBinding));
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.DownArrow, historyKeyBinding));
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.OemComma, historyKeyBinding));
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.OemPeriod, historyKeyBinding));
                activeEditor.KeyBindings.Add(new KeyBinding(ConsoleKey.S, saveKeyBinding));
                activeEditor.AddDefaultKeybindings();
                activeEditor.PaintLineCounter();
                activeEditor.EnterInputLoop();
                normalizeEditorState(activeEditor);
                Console.CursorVisible = true;
                Console.WriteLine();
                Console.CursorLeft = 5;
                Console.ForegroundColor = ConsoleColor.Gray;
                string expr = activeEditor.Buffer.ToString();
                pastSources.Add(expr);
                pastSourceInd = pastSources.Count;
                if (openLast == null)
                {
                    IEnumerable<LispToken> exprResult = RuntimeContext.EvaluateExpressions(expr, consString);
                    if (exprResult != null)
                    {
                        foreach (LispToken token in exprResult)
                        {
                            Console.CursorLeft = 2;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("=> ");
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine(token.ToString().Replace("\n", "\n     "));
                        }
                    }
                }
                Console.WriteLine();
            }
        }

        private bool historyKeyBinding(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            if (keyInfo.Key == ConsoleKey.OemComma || keyInfo.Key == ConsoleKey.OemPeriod)
            {
                if (!keyInfo.Modifiers.HasFlag(ConsoleModifiers.Alt))
                {
                    return false;
                }
            }
            else if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (activeEditor.LineCount > 1)
                {
                    return false;
                }
            } else
            {
                return false;
            }

            if (keyInfo.Key == ConsoleKey.UpArrow || keyInfo.Key == ConsoleKey.OemComma)
            {
                if (pastSourceInd - 1 >= 0 && pastSourceInd - 1 < pastSources.Count)
                {
                    pastSourceInd--;
                    activeEditor.SetText(pastSources[pastSourceInd]);
                }
            } else if (keyInfo.Key == ConsoleKey.DownArrow || keyInfo.Key == ConsoleKey.OemPeriod)
            {
                if (pastSourceInd + 1 == pastSources.Count)
                {
                    pastSourceInd++;
                    activeEditor.SetText("()");
                    activeEditor.CursorPosition = 1;
                } else if (pastSourceInd + 1 < pastSources.Count)
                {
                    pastSourceInd++;
                    activeEditor.SetText(pastSources[pastSourceInd]);
                }
            }
            return true;
        }

        private bool enterKeyBinding(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            if ((keyInfo.Key == ConsoleKey.Enter && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift)) || (keyInfo.Key == ConsoleKey.R && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control)))
            {
                activeEditor.BreakInputLoop();
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool saveKeyBinding(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                saveLast = editor.Buffer.ToString();
                openLast = "(save \"\")";
                openLastPos = 7;
                activeEditor.BreakInputLoop();
                return true;
            } else
            {
                return false;
            }
        }

        private void normalizeEditorState(ConsoleLispEditor editor)
        {
            editor.TipSource = TipContent.NoContent;
            editor.UpdateTip();
            editor.AutocompleteSource = null;
            editor.UpdateAutoComplete();
            editor.CursorPosition = editor.Buffer.Length;
            editor.ScrollCursorIntoView();
            (int x, int y) endPos = editor.XYfromBufferPosition(editor.CursorPosition);
            Console.SetCursorPosition(editor.WindowX + endPos.x, editor.WindowY + endPos.y);
        }

        private IEnumerable<LispToken> setModulePath(RuntimeContext runtimeContext)
        {
            string moduleDirPath = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("path"), LispDataType.String).Value;
            if (Directory.Exists(moduleDirPath))
            {
                runtimeContext.ModuleSearchDirectory = moduleDirPath;
            } else
            {
                Console.WriteLine("nlshell-setmodulepath: module directory not set because path does not exit");
            }
            yield break;
        }
    }
}

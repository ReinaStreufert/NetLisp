using LispDNBridgeNativeSource;
using LispTableNativeSource;
using NetLisp.Data;
using NetLisp.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlshell
{
    public partial class ConsoleLispEditor
    {
        delegate void characterInsertedEvent(int oldCursorPosition, char character, bool selectionReplaced);
        delegate void backspaceEvent(int oldCursorPosition, bool selectionReplaced);
        delegate void linebreakEvent(int oldCursorPosition, bool selectionReplaced, List<int> newLinebreaks);
        delegate void cursorNavigateEvent(int oldCursorPosition, bool selectionUpdated);
        delegate void sourceProcessEvent();
        delegate void inputProcessEvent();

        event characterInsertedEvent characterInsertedScriptEvent;
        event backspaceEvent backspaceScriptEvent;
        event linebreakEvent linebreakScriptEvent;
        event cursorNavigateEvent cursorNavigateScriptEvent;
        event sourceProcessEvent sourceProcessScriptEvent;
        event inputProcessEvent inputProcessedScriptEvent;

        public class ScriptHost
        {
            public RuntimeContext RuntimeContext { get; private set; }
            private ConsoleLispEditor editor = null;
            public ConsoleLispEditor Editor
            {
                get
                {
                    return editor;
                }
                set
                {
                    if (editor != null)
                    {
                        editor.characterInsertedScriptEvent -= editor_characterInsertedScriptEvent;
                        editor.backspaceScriptEvent -= editor_backspaceScriptEvent;
                        editor.linebreakScriptEvent -= editor_linebreakScriptEvent;
                        editor.cursorNavigateScriptEvent -= editor_cursorNavigateScriptEvent;
                        editor.sourceProcessScriptEvent -= editor_sourceProcessScriptEvent;
                        editor.inputProcessedScriptEvent -= editor_inputProcessedScriptEvent;
                    }
                    editor = value;
                    editor.characterInsertedScriptEvent += editor_characterInsertedScriptEvent;
                    editor.backspaceScriptEvent += editor_backspaceScriptEvent;
                    editor.linebreakScriptEvent += editor_linebreakScriptEvent;
                    editor.cursorNavigateScriptEvent += editor_cursorNavigateScriptEvent;
                    editor.sourceProcessScriptEvent += editor_sourceProcessScriptEvent;
                    editor.inputProcessedScriptEvent += editor_inputProcessedScriptEvent;
                    raiseScriptEvent(scriptinitSubscribers, new DotnetInstance(editor));
                }
            }

            public ScriptHost(string moduleSearchPath = "")
            {
                RuntimeContext = new RuntimeContext(SandboxingFlags.AllowArbitraryFileLoad);
                if (moduleSearchPath != "")
                {
                    RuntimeContext.ModuleSearchDirectory = moduleSearchPath;
                }
                RuntimeContext.RuntimeError += runtimeContext_RuntimeError;
                RuntimeContext.SyntaxError += runtimeContext_SyntaxError;

                Scope global = RuntimeContext.Scopes.GlobalScope;
                global.Define("bindkey", new LispFunction(new NativeExecutableBody(bindkey), ScopeStack.ConstructFromScope(global), null, new LispSymbol("key"), new LispSymbol("callback")));
            }

            private List<LispFunction> scriptinitSubscribers = new List<LispFunction>();
            private List<LispFunction> characterInsertedSubscribers = new List<LispFunction>();
            private List<LispFunction> backspaceSubscribers = new List<LispFunction>();
            private List<LispFunction> linebreakSubscribers = new List<LispFunction>();
            private List<LispFunction> cursorNavigateSubscribers = new List<LispFunction>();
            private List<LispFunction> sourceProcessSubscribers = new List<LispFunction>();
            private List<LispFunction> inputProcessedSubscribers = new List<LispFunction>();

            public bool AddScript(string scriptPath)
            {
                LispToken loadResult;
                if (RuntimeContext.LoadSourceFile(scriptPath, out loadResult) != FileLoadResult.Success)
                {
                    return false;
                }
                if (loadResult.Type == LispDataType.ExtendedType && ((ExtendedLispToken)loadResult).ExtendedTypeInfo.ExtendedTypeGuid == LispTable.TableExtendedTypeInfo.ExtendedTypeGuid)
                {
                    LispTable scriptTable = (LispTable)loadResult;
                    LispSymbol scriptinit = new LispSymbol("scriptinit");
                    LispSymbol charinsert = new LispSymbol("charinsert");
                    LispSymbol backspace = new LispSymbol("backspace");
                    LispSymbol linebreak = new LispSymbol("linebreak");
                    LispSymbol cursornav = new LispSymbol("cursornav");
                    LispSymbol sourceproc = new LispSymbol("sourceproc");
                    LispSymbol inputproc = new LispSymbol("inputproc");
                    LispFunction scriptinitSubscriber = null;
                    if (scriptTable.ContainsKey(scriptinit))
                    {
                        LispToken scriptinitVal = scriptTable[scriptinit];
                        if (scriptinitVal.Type == LispDataType.Function)
                        {
                            scriptinitSubscriber = (LispFunction)scriptinitVal;
                            scriptinitSubscribers.Add(scriptinitSubscriber);
                        }
                    }
                    if (scriptTable.ContainsKey(charinsert))
                    {
                        LispToken charinsertVal = scriptTable[charinsert];
                        if (charinsertVal.Type == LispDataType.Function)
                        {
                            characterInsertedSubscribers.Add((LispFunction)charinsertVal);
                        }
                    }
                    if (scriptTable.ContainsKey(backspace))
                    {
                        LispToken backspaceVal = scriptTable[backspace];
                        if (backspaceVal.Type == LispDataType.Function)
                        {
                            backspaceSubscribers.Add((LispFunction)backspaceVal);
                        }
                    }
                    if (scriptTable.ContainsKey(linebreak))
                    {
                        LispToken linebreakVal = scriptTable[linebreak];
                        if (linebreakVal.Type == LispDataType.Function)
                        {
                            linebreakSubscribers.Add((LispFunction)linebreakVal);
                        }
                    }
                    if (scriptTable.ContainsKey(cursornav))
                    {
                        LispToken cursornavVal = scriptTable[cursornav];
                        if (cursornavVal.Type == LispDataType.Function)
                        {
                            cursorNavigateSubscribers.Add((LispFunction)cursornavVal);
                        }
                    }
                    if (scriptTable.ContainsKey(sourceproc))
                    {
                        LispToken sourceprocVal = scriptTable[sourceproc];
                        if (sourceprocVal.Type == LispDataType.Function)
                        {
                            sourceProcessSubscribers.Add((LispFunction)sourceprocVal);
                        }
                    }
                    if (scriptTable.ContainsKey(inputproc))
                    {
                        LispToken inputprocVal = scriptTable[inputproc];
                        if (inputprocVal.Type == LispDataType.Function)
                        {
                            inputProcessedSubscribers.Add((LispFunction)inputprocVal);
                        }
                    }
                    if (scriptinitSubscriber != null)
                    {
                        raiseScriptEvent(scriptinitSubscriber, new DotnetInstance(editor));
                    }
                    return true;
                } else
                {
                    return false;
                }
            }

            private void raiseScriptEvent(List<LispFunction> subscribers, params LispToken[] args)
            {
                List<LispToken> callExpressions = new List<LispToken>();
                foreach (LispFunction function in subscribers)
                {
                    callExpressions.AddRange(callExpression(function, args));
                }
                RuntimeContext.EvaluateExpressions(callExpressions);
            }
            private void raiseScriptEvent(LispFunction subscriber, params LispToken[] args)
            {
                RuntimeContext.EvaluateExpressions(callExpression(subscriber, args));
            }

            private void editor_cursorNavigateScriptEvent(int oldCursorPosition, bool selectionUpdated)
            {
                raiseScriptEvent(cursorNavigateSubscribers, new LispNumber(oldCursorPosition), new LispBoolean(selectionUpdated));
            }

            private void editor_linebreakScriptEvent(int oldCursorPosition, bool selectionReplaced, List<int> newLinebreaks)
            {
                raiseScriptEvent(linebreakSubscribers, new LispNumber(oldCursorPosition), new LispBoolean(selectionReplaced), new DotnetInstance(newLinebreaks));
            }

            private void editor_backspaceScriptEvent(int oldCursorPosition, bool selectionReplaced)
            {
                raiseScriptEvent(backspaceSubscribers, new LispNumber(oldCursorPosition), new LispBoolean(selectionReplaced));
            }

            private void editor_characterInsertedScriptEvent(int oldCursorPosition, char character, bool selectionReplaced)
            {
                raiseScriptEvent(characterInsertedSubscribers, new LispNumber(oldCursorPosition), new LispString(character.ToString()), new LispBoolean(selectionReplaced));
            }

            private void editor_sourceProcessScriptEvent()
            {
                raiseScriptEvent(sourceProcessSubscribers);
            }

            private void editor_inputProcessedScriptEvent()
            {
                raiseScriptEvent(inputProcessedSubscribers);
            }

            private void runtimeContext_SyntaxError(NetLisp.Text.SyntaxError err)
            {
                Console.WriteLine(err.ToString());
            }

            private void runtimeContext_RuntimeError(RuntimeError err)
            {
                Console.WriteLine(err.ToString());
            }

            private IEnumerable<LispToken> bindkey(RuntimeContext runtimeContext)
            {
                LispString keyName = runtimeContext.Assert<LispString>(runtimeContext.Scopes.CurrentScope.Get("key"), LispDataType.String);
                LispFunction callback = runtimeContext.Assert<LispFunction>(runtimeContext.Scopes.CurrentScope.Get("callback"), LispDataType.Function);
                ConsoleKey key;
                if (!Enum.TryParse(keyName.Value, out key))
                {
                    runtimeContext.RaiseRuntimeError(keyName, RuntimeErrorType.ArgumentMismatchError, "key name given was not the name of a valid key");
                }
                Editor.KeyBindings.Add(new KeyBinding(key, (ConsoleLispEditor editor, ConsoleKeyInfo keyInfo) =>
                {
                    IEnumerable<LispToken> evalResult = runtimeContext.EvaluateExpressions(callExpression(callback, new DotnetInstance(keyInfo)));
                    LispToken? result;
                    if (evalResult == null)
                    {
                        result = null;
                    } else
                    {
                        result = evalResult.FirstOrDefault((LispToken?)null);
                    }
                    if (result != null && result.Type == LispDataType.Boolean)
                    {
                        return ((LispBoolean)result).Value;
                    } else
                    {
                        return false;
                    }
                }));
                yield break;
            }

            private IEnumerable<LispToken> callExpression(LispFunction function, params LispToken[] args)
            {
                yield return new LispList(args.Prepend(function).ToList());
            }
        }
    }
}

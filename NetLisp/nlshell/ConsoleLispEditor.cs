using NetLisp.Data;
using NetLisp.Text;
using NetLisp.Text.CodeAnalysis;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TextCopy;

namespace nlshell
{
    // this file is way too fucking big. jesus christ. i have a problem. i need serious help.
    // someone needs to help me. help. oh my god.
    public partial class ConsoleLispEditor // who knew an entire fucking text editor would need more than one file. totally not obvious to most people at all.
    {
        public SourceAnalyzer AnalysisProvider { get; set; }
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int ScrollX { get; set; } = 0;
        public int ScrollY { get; set; } = 0;
        public bool CreepUp { get; set; } = false;
        public int BottomMargin { get; set; } = 0;

        public int CursorPosition { get; set; } = 0;
        public int SelectionStart { get; set; } = -1;
        public int SelectionLength { get; set; } = 0;

        public int LineCount
        {
            get
            {
                return linebreaks.Count + 1;
            }
        }

        private ConsoleColor[] defaultCharacterClassMapping = new ConsoleColor[(int)CharacterMapTokenClass.Whitespace + 1];
        private List<int> linebreaks;
        private bool escapeInput = false;

        private int lastAutocompleteX = -1; // scroll independent
        private int lastAutocompleteY = -1; // scroll independent
        private int lastAutocompleteHeight = -1;
        private AutocompleteOption[]? lastAutocompleteOptions;
        private int autocompleteScroll = 0;
        private int autocompleteSelectedItem = -1;

        private int lastTipX = -1; // scroll independent
        private int lastTipY = -1; // scroll independent
        private int lastTipWidth = -1;
        private int lastTipHeight = -1;
        private string[] tipAnnotationLines = null;

        public SourceAnalysis BufferAnalysis { get; private set; }
        public CharacterMap<ConsoleColor> BufferSyntaxHighlighting { get; private set; } = null;
        public StringBuilder Buffer { get; private set; }
        public List<KeyBinding> KeyBindings { get; set; } = new List<KeyBinding>();
        public IEnumerable<AutocompleteOption>? AutocompleteSource { get; set; } = null;
        public TipContent TipSource { get; set; } = TipContent.NoContent;

        private const ConsoleColor highlightColor = ConsoleColor.White;
        private const int autocompleteWidth = 20;
        private const int maxAutocompleteHeight = 10;
        private const int maxTipWidth = 60;

        public ConsoleLispEditor(SourceAnalyzer analysisProvider, int x, int y, string startText = "")
        {
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.String] = ConsoleColor.Yellow;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.StringEscaped] = ConsoleColor.DarkYellow;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.Number] = ConsoleColor.Red;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.Symbol] = ConsoleColor.DarkCyan;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.OpenParen] = ConsoleColor.DarkGray;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.CloseParen] = ConsoleColor.DarkGray;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.Comment] = ConsoleColor.DarkGreen;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.Quote] = ConsoleColor.DarkGray;
            defaultCharacterClassMapping[(int)CharacterMapTokenClass.Whitespace] = ConsoleColor.Gray;
            AnalysisProvider = analysisProvider;
            Buffer = new StringBuilder(startText);
            WindowX = x;
            WindowY = y;
            WindowWidth = Console.WindowWidth - x;
            WindowHeight = Console.WindowHeight - y;
            linebreaks = new List<int>();
            for (int i = 0; i < startText.Length; i++)
            {
                if (startText[i] == '\n')
                {
                    linebreaks.Add(i);
                }
            }
            ProcessSourceChange();
            Console.CursorVisible = false;
            InvalidateRegion(0, 0, WindowWidth, WindowHeight);
        }

        public void EnterInputLoop()
        {
            while (!escapeInput)
            {
                inputProcessedScriptEvent?.Invoke();
                UpdateTip();
                UpdateAutoComplete();
                PresentCursor();
                ConsoleKeyInfo key = Console.ReadKey(true);
                Console.CursorVisible = false;
                if (AutocompleteSource != null && (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.Tab))
                {
                    bool cleanBreak;
                    while (autocompleteProc(key, out cleanBreak))
                    {
                        PresentCursor();
                        key = Console.ReadKey(true);
                        Console.CursorVisible = false;
                    }
                    if (cleanBreak)
                    {
                        AutocompleteSource = null;
                        continue;
                    }
                }
                AutocompleteSource = null;
                TipSource = TipContent.NoContent;
                bool keyHandled = false;
                foreach (KeyBinding keyBinding in KeyBindings)
                {
                    if (keyBinding.Key == key.Key && keyBinding.Callback(this, key))
                    {
                        keyHandled = true;
                        break;
                    }
                }
                if (!keyHandled && !char.IsControl(key.KeyChar))
                {
                    onInsertCharacter(this, key);
                }
            }
            escapeInput = false;
        }

        public void AddDefaultKeybindings()
        {
            KeyBindings.Add(new KeyBinding(ConsoleKey.LeftArrow, onNavigate));
            KeyBindings.Add(new KeyBinding(ConsoleKey.RightArrow, onNavigate));
            KeyBindings.Add(new KeyBinding(ConsoleKey.UpArrow, onNavigate));
            KeyBindings.Add(new KeyBinding(ConsoleKey.DownArrow, onNavigate));
            KeyBindings.Add(new KeyBinding(ConsoleKey.Backspace, onBackspace));
            KeyBindings.Add(new KeyBinding(ConsoleKey.Enter, onLinebreak));
            KeyBindings.Add(new KeyBinding(ConsoleKey.A, onSelectAll));
        }

        private bool onInsertCharacter(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            bool selectionReplaced = EliminateSelection();
            Buffer.Insert(CursorPosition, keyInfo.KeyChar);
            int oldCursorPos = CursorPosition;
            CursorPosition++;
            int oldBufferLength = Buffer.Length;
            characterInsertedScriptEvent?.Invoke(oldCursorPos, keyInfo.KeyChar, selectionReplaced);
            MoveLineBreaks(oldCursorPos, 1 + (Buffer.Length - oldBufferLength));
            ProcessSourceChange();
            (int x, int y) cursorScreenPos = XYfromBufferPosition(oldCursorPos);
            InvalidateRegion(cursorScreenPos.x, cursorScreenPos.y, WindowWidth - cursorScreenPos.x, 1);
            ScrollCursorIntoView();
            return true;
        }
        private bool onNavigate(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            int oldPosition = CursorPosition;
            if (keyInfo.Key == ConsoleKey.LeftArrow)
            {
                if (CursorPosition > 0)
                {
                    CursorPosition--;
                    if (Buffer[CursorPosition] == '\n' && CursorPosition > 0 && Buffer[CursorPosition - 1] == '\r')
                    {
                        CursorPosition--;
                    }
                }
            } else if (keyInfo.Key == ConsoleKey.RightArrow)
            {
                if (CursorPosition < Buffer.Length)
                {
                    if (Buffer[CursorPosition] == '\r' && Buffer[CursorPosition + 1] == '\n')
                    {
                        CursorPosition += 2;
                    } else
                    {
                        CursorPosition++;
                    }
                }
            } else if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                if (cursorScreenPos.y + ScrollY > 0)
                {
                    int newLine = cursorScreenPos.y + ScrollY - 1;
                    int newLineLength = lineMaxLength(newLine);
                    if (cursorScreenPos.x < newLineLength)
                    {
                        CursorPosition = BufferPositionFromXY(cursorScreenPos.x, newLine - ScrollY);
                    } else
                    {
                        CursorPosition = BufferPositionFromXY(newLineLength - 1, newLine - ScrollY);
                    }
                }
            } else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                if (cursorScreenPos.y + ScrollY + 1 < linebreaks.Count + 1)
                {
                    int newLine = cursorScreenPos.y + ScrollY + 1;
                    int newLineLength = lineMaxLength(newLine);
                    if (cursorScreenPos.x < newLineLength)
                    {
                        CursorPosition = BufferPositionFromXY(cursorScreenPos.x, newLine - ScrollY);
                    }
                    else
                    {
                        CursorPosition = BufferPositionFromXY(newLineLength - 1, newLine - ScrollY);
                    }
                }
            }
            bool hasShift = keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            if (hasShift)
            {
                (int start, int length) newSelection;
                if (SelectionStart > -1)
                {
                    if (oldPosition == SelectionStart)
                    {
                        newSelection = rangeFromPositions(CursorPosition, SelectionStart + SelectionLength);
                    } else if (oldPosition == SelectionStart + SelectionLength)
                    {
                        newSelection = rangeFromPositions(SelectionStart, CursorPosition);
                    } else
                    {
                        newSelection = rangeFromPositions(oldPosition, CursorPosition);
                    }
                } else
                {
                    newSelection = rangeFromPositions(oldPosition, CursorPosition);
                }
                UpdateSelection(newSelection.start, newSelection.length);
            }
            cursorNavigateScriptEvent?.Invoke(oldPosition, hasShift);
            ScrollCursorIntoView();
            return true;
        }

        private bool onBackspace(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            if (CursorPosition > 0)
            {
                int oldCursorPos = CursorPosition;
                if (!EliminateSelection())
                {
                    CursorPosition--;
                    bool lineDeleted = false;
                    int charsDeleted = 1;
                    if (Buffer[CursorPosition] == '\n')
                    {
                        linebreaks.Remove(CursorPosition);
                        if (CursorPosition > 0 && Buffer[CursorPosition - 1] == '\r')
                        {
                            CursorPosition--;
                            charsDeleted = 2;
                            Buffer.Remove(CursorPosition, 2);
                        }
                        else
                        {
                            Buffer.Remove(CursorPosition, 1);
                        }
                        lineDeleted = true;
                    }
                    else
                    {
                        Buffer.Remove(CursorPosition, 1);
                    }
                    int oldBufferLength = Buffer.Length;
                    backspaceScriptEvent?.Invoke(oldCursorPos, false);
                    MoveLineBreaks(CursorPosition, -charsDeleted + (Buffer.Length - oldBufferLength));
                    ProcessSourceChange();
                    (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                    InvalidateRegion(cursorScreenPos.x, cursorScreenPos.y, WindowWidth - cursorScreenPos.x, 1, true);
                    if (lineDeleted)
                    {
                        InvalidateRegion(0, cursorScreenPos.y + 1, WindowWidth, WindowHeight - cursorScreenPos.y, true);
                        EraseLineCounterRows(XYfromBufferPosition(Buffer.Length).y + 1, 1);
                    }
                } else
                {
                    backspaceScriptEvent?.Invoke(oldCursorPos, true);
                }
                ScrollCursorIntoView();
            }
            return true;
        }
        private bool onLinebreak(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            bool selectionReplaced = EliminateSelection();
            Buffer.Insert(CursorPosition, "\r\n");
            int newLinebreakPosition = CursorPosition + 1;
            int oldCursorPos = CursorPosition;
            int insertIndex = linebreaks.FindLastIndex((int breakpos) => { return breakpos < newLinebreakPosition; }) + 1;
            CursorPosition += 2;
            int oldBufferLength = Buffer.Length;
            List<int> newLinebreaks = new List<int>() { newLinebreakPosition };
            linebreakScriptEvent?.Invoke(oldCursorPos, selectionReplaced, newLinebreaks);
            MoveLineBreaks(oldCursorPos, 2 + (Buffer.Length - oldBufferLength));
            linebreaks.InsertRange(insertIndex, newLinebreaks);
            ProcessSourceChange();
            (int x, int y) cursorScreenPos = XYfromBufferPosition(oldCursorPos);
            InvalidateRegion(cursorScreenPos.x, cursorScreenPos.y, WindowWidth - cursorScreenPos.x, 1, true);
            InvalidateRegion(0, cursorScreenPos.y + 1, WindowWidth, WindowHeight - cursorScreenPos.y, true);
            ScrollCursorIntoView();
            PaintLineCounter(XYfromBufferPosition(Buffer.Length).y - (newLinebreaks.Count - 1));
            return true;
        }
        private bool onSelectAll(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo)
        {
            if (keyInfo.Modifiers == ConsoleModifiers.Control)
            {
                UpdateSelection(0, Buffer.Length);
                CursorPosition = Buffer.Length;
                ScrollCursorIntoView();
                return true;
            } else
            {
                return false;
            }
        }

        public void ProcessSourceChange()
        {
            BufferAnalysis = AnalysisProvider.Analyze(Buffer.ToString());
            CharacterMap<ConsoleColor> newSyntaxHighlighting = new CharacterMap<ConsoleColor>();
            newSyntaxHighlighting.Length = BufferAnalysis.CharacterMap.Length;
            foreach (CharacterMap<CharacterMapTokenClass>.CharacterRange range in BufferAnalysis.CharacterMap.Ranges)
            {
                var newRange = new CharacterMap<ConsoleColor>.CharacterRange(range.Start, range.Length, defaultCharacterClassMapping[(int)range.Classification]);
                newSyntaxHighlighting.Ranges.Add(newRange);
                for (int i = 0; i < newRange.Length; i++)
                {
                    newSyntaxHighlighting.RangeMap.Add(newRange);
                }
            }
            BufferSyntaxHighlighting = newSyntaxHighlighting;
            sourceProcessScriptEvent?.Invoke();
        }

        public void BreakInputLoop()
        {
            escapeInput = true;
        }

        public void SetText(string newText)
        {
            Buffer = new StringBuilder(newText);
            linebreaks = new List<int>();
            for (int i = 0; i < newText.Length; i++)
            {
                if (newText[i] == '\n')
                {
                    linebreaks.Add(i);
                }
            }
            ProcessSourceChange();
            InvalidateRegion(0, 0, WindowWidth, WindowHeight, true);
            CursorPosition = Buffer.Length;
            SelectionStart = -1;
            ScrollCursorIntoView();
            EraseLineCounterRows(0, WindowHeight);
            PaintLineCounter(0);
        }

        public void InvalidateRegion(int x, int y, int width, int height, bool paintBlankSpace = false, Func<int, int, bool> filter = null)
        {
            if (x < 0)
            {
                width += x;
                x = 0;
            }
            if (y < 0)
            {
                height += y;
                y = 0;
            }
            int cursorX = -1;
            int cursorY = -1;
            ConsoleColor cursorColor = Console.ForegroundColor;
            ConsoleColor backColor = Console.BackgroundColor;
            for (int drawY = y; drawY < y + height; drawY++)
            {
                if (drawY >= WindowHeight)
                {
                    break;
                }
                for (int drawX = x; drawX < x + width; drawX++)
                {
                    if (drawX >= WindowWidth)
                    {
                        break;
                    }
                    if (filter != null && !filter(drawX, drawY))
                    {
                        continue;
                    }
                    int bufferPos = BufferPositionFromXY(drawX, drawY);
                    if (cursorX != drawX || cursorY != drawY)
                    {
                        Console.SetCursorPosition(drawX + WindowX, drawY + WindowY);
                        cursorX = drawX;
                        cursorY = drawY;
                    }
                    if (bufferPos < 0 || bufferPos >= Buffer.Length) // = end of text on the specified line
                    {
                        if (paintBlankSpace)
                        {
                            if (backColor != ConsoleColor.Black)
                            {
                                Console.BackgroundColor = ConsoleColor.Black;
                                backColor = ConsoleColor.Black;
                            }
                            Console.Write(" ");
                            cursorX++;
                            continue; // next position
                        } else
                        {
                            break; // next line
                        }
                    }
                    char bufferChar = this.Buffer[bufferPos];
                    if (bufferChar == '\r' || bufferChar == '\n')
                    {
                        if (backColor != ConsoleColor.Black)
                        {
                            Console.BackgroundColor = ConsoleColor.Black;
                            backColor = ConsoleColor.Black;
                        }
                        Console.Write(" ");
                        cursorX++;
                        continue;
                    }
                    if (bufferPos < BufferSyntaxHighlighting.Length)
                    {
                        CharacterMap<ConsoleColor>.CharacterRange range = BufferSyntaxHighlighting[bufferPos];
                        if (range.Classification != cursorColor)
                        {
                            Console.ForegroundColor = range.Classification;
                            cursorColor = range.Classification;
                        }
                    } else
                    {
                        if (cursorColor != ConsoleColor.Gray)
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            cursorColor = ConsoleColor.Gray;
                        }
                    }
                    bool selected = IsInSelection(bufferPos);
                    if (selected && backColor != highlightColor)
                    {
                        Console.BackgroundColor = highlightColor;
                        backColor = highlightColor;
                    } else if (!selected && backColor != ConsoleColor.Black)
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                        backColor = ConsoleColor.Black;
                    }
                    Console.Write(bufferChar);
                    cursorX++;
                }
            }
            if (backColor != ConsoleColor.Black)
            {
                Console.BackgroundColor = ConsoleColor.Black;
            }
        }

        public void InvalidateBufferRange(int bufferStart, int bufferLength, Func<int, bool>? filter = null)
        {
            int cursorX = -1;
            int cursorY = -1;
            ConsoleColor cursorColor = Console.ForegroundColor;
            ConsoleColor backColor = Console.BackgroundColor;
            for (int i = bufferStart; i < bufferStart + bufferLength; i++)
            {
                char bufferChar = Buffer[i];
                if ((filter != null && !filter(i)) || bufferChar == '\n' || bufferChar == '\r')
                {
                    continue;
                }
                (int x, int y) screenPos = XYfromBufferPosition(i);
                if (screenPos.x >= 0 && screenPos.y >= 0 && screenPos.x < WindowWidth && screenPos.y < WindowHeight)
                {
                    if (cursorX != screenPos.x || cursorY != screenPos.y)
                    {
                        Console.SetCursorPosition(screenPos.x + WindowX, screenPos.y + WindowY);
                        cursorX = screenPos.x;
                        cursorY = screenPos.y;
                    }
                    if (i < BufferSyntaxHighlighting.Length)
                    {
                        CharacterMap<ConsoleColor>.CharacterRange range = BufferSyntaxHighlighting[i];
                        if (range.Classification != cursorColor)
                        {
                            Console.ForegroundColor = range.Classification;
                            cursorColor = range.Classification;
                        }
                    }
                    else
                    {
                        if (cursorColor != ConsoleColor.Gray)
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                            cursorColor = ConsoleColor.Gray;
                        }
                    }
                    bool selected = IsInSelection(i);
                    if (selected && backColor != highlightColor)
                    {
                        Console.BackgroundColor = highlightColor;
                        backColor = highlightColor;
                    }
                    else if (!selected && backColor != ConsoleColor.Black)
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                        backColor = ConsoleColor.Black;
                    }
                    Console.Write(bufferChar);
                    cursorX++;
                }
            }
        }
        public void PaintLineCounter(int startY = 0)
        {
            int lineCounterX = WindowX - 5;
            int lineCounterY = WindowY;
            int yzeroLine = ScrollY;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Red;
            for (int y = startY; y < WindowHeight; y++)
            {
                if (yzeroLine + y >= linebreaks.Count + 1)
                {
                    break;
                }
                Console.SetCursorPosition(lineCounterX, lineCounterY + y);
                string lineNumber = (yzeroLine + y + 1).ToString();
                int leftPad = 4 - lineNumber.Length;
                Console.Write(new string(' ', leftPad) + lineNumber + " ");
            }
        }
        public void EraseLineCounterRows(int startY, int yLength)
        {
            int lineCounterX = WindowX - 5;
            int lineCounterY = WindowY;
            Console.BackgroundColor = ConsoleColor.Black;
            for (int y = startY; y < startY + yLength; y++)
            {
                if (y >= WindowHeight)
                {
                    break;
                } else if (y < 0)
                {
                    y = -1;
                    continue;
                }
                Console.SetCursorPosition(lineCounterX, lineCounterY + y);
                Console.Write("     ");
            }
        }

        public void PresentCursor()
        {
            (int x, int y) screenpos = XYfromBufferPosition(CursorPosition);
            Console.SetCursorPosition(screenpos.x + WindowX, screenpos.y + WindowY);
            Console.CursorVisible = true;
        }
         
        public void ScrollRelativeXY(int x, int y)
        {
            int oldLineCounterEndY;
            int newLineCounterEndY;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Math.Abs(x) >= WindowWidth || Math.Abs(y) >= WindowHeight)
            {
                oldLineCounterEndY = (linebreaks.Count + 1) - ScrollY;
                ScrollX += x;
                ScrollY += y;
                InvalidateRegion(0, 0, WindowWidth, WindowHeight, true);
                PaintLineCounter();
                newLineCounterEndY = (linebreaks.Count + 1) - ScrollY;
                if (newLineCounterEndY < WindowHeight && oldLineCounterEndY - newLineCounterEndY > 0)
                {
                    int eraseStart = newLineCounterEndY;
                    EraseLineCounterRows(eraseStart, WindowHeight - eraseStart);
                }
                return;
            }
            int moveSrcX = WindowX;
            int moveSrcY = WindowY;
            int moveDstX = WindowX;
            int moveDstY = WindowY;
            int moveWidth = WindowWidth;
            int moveHeight = WindowHeight;
            int invalX = 0;
            int invalY = 0;
            int invalWidth = 0;
            int invalHeight = 0;
            if (x < 0)
            {
                moveSrcX = WindowX;
                moveWidth = WindowWidth + x;
                moveDstX = WindowX - x;
                invalX = 0;
                invalY = 0;
                invalWidth = -x;
                invalHeight = WindowHeight;
            } else if (x > 0)
            {
                moveSrcX = WindowX + x;
                moveWidth = WindowWidth - x;
                moveDstX = WindowX;
                invalX = WindowWidth - x;
                invalY = 0;
                invalWidth = x;
                invalHeight = WindowHeight;
            }
            if (y < 0)
            {
                moveSrcY = WindowY;
                moveHeight = WindowHeight + y;
                moveDstY = WindowY - y;
                invalX = 0;
                invalY = 0;
                invalWidth = WindowWidth;
                invalHeight = -y;
            } else if (y > 0)
            {
                moveSrcY = WindowY + y;
                moveHeight = WindowHeight - y;
                moveDstY = WindowY;
                invalX = 0;
                invalY = WindowHeight - y;
                invalWidth = WindowWidth;
                invalHeight = y;
            }
            Console.BackgroundColor = ConsoleColor.Black;
            Console.MoveBufferArea(moveSrcX, moveSrcY, moveWidth, moveHeight, moveDstX, moveDstY);
            oldLineCounterEndY = (linebreaks.Count + 1) - ScrollY;
            ScrollX += x;
            ScrollY += y;
            InvalidateRegion(invalX, invalY, invalWidth, invalHeight);
            PaintLineCounter();
            newLineCounterEndY = (linebreaks.Count + 1) - ScrollY;
            if (newLineCounterEndY < WindowHeight && oldLineCounterEndY - newLineCounterEndY > 0)
            {
                int eraseStart = newLineCounterEndY;
                EraseLineCounterRows(eraseStart, WindowHeight - eraseStart);
            }
        }

        public void UpdateSelection(int newSelectionStart, int newSelectionLength)
        {
            int oldSelectionStart = SelectionStart;
            int oldSelectionLength = SelectionLength;
            SelectionStart = newSelectionStart;
            SelectionLength = newSelectionLength;
            if (oldSelectionStart > -1)
                InvalidateBufferRange(oldSelectionStart, oldSelectionLength, (int position) => { return !IsInSelection(position); });
            if (newSelectionStart > -1) 
                InvalidateBufferRange(SelectionStart, SelectionLength, (int position) => { return !isInRange(oldSelectionStart, oldSelectionLength, position); });
        }

        // if there is a selection, either deselects it if the cursor is not at either edge of the
        // selection OR deselects, deletes the selections contents, and moves the cursor to the left
        // edge of the old selection if the cursor is at either edge
        public bool EliminateSelection() // returns whether deletion occurred
        {
            if (SelectionStart > -1)
            {
                if (CursorPosition == SelectionStart || CursorPosition == SelectionStart + SelectionLength)
                {
                    int oldSelectionStart = SelectionStart;
                    int oldSelectionLength = SelectionLength;
                    UpdateSelection(-1, 0);
                    // search for linebreaks in selection and remove linebreak positions
                    int linesDeleted = 0;
                    for (int i = oldSelectionStart; i < oldSelectionStart + oldSelectionLength; i++)
                    {
                        if (Buffer[i] == '\n')
                        {
                            linesDeleted++;
                            linebreaks.Remove(i);
                        }
                    }
                    MoveLineBreaks(oldSelectionStart, -oldSelectionLength);
                    // delete and set cursor position
                    Buffer.Remove(oldSelectionStart, oldSelectionLength);
                    CursorPosition = oldSelectionStart;
                    ProcessSourceChange();
                    (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                    InvalidateRegion(cursorScreenPos.x, cursorScreenPos.y, WindowWidth - cursorScreenPos.x, 1, true);
                    if (linesDeleted > 0)
                    {
                        InvalidateRegion(0, cursorScreenPos.y + 1, WindowWidth, WindowHeight - cursorScreenPos.y, true);
                        EraseLineCounterRows(XYfromBufferPosition(Buffer.Length).y + 1, linesDeleted);
                    }
                    return true;
                } else
                {
                    UpdateSelection(-1, 0);
                    return false;
                }
            }
            return false;
        }

        public bool IsInSelection(int position)
        {
            return isInRange(SelectionStart, SelectionLength, position);
        }

        private bool isInRange(int start, int length, int position)
        {
            if (start < 0)
            {
                return false;
            } else
            {
                return (position >= start && position < start + length);
            }
        }

        private (int start, int length) rangeFromPositions(int a, int b)
        {
            int start = Math.Min(a, b);
            int length = Math.Max(a, b) - start;
            return (start, length);
        }

        public void MoveLineBreaks(int startPosition, int relativeChange)
        {
            for (int i = 0; i < linebreaks.Count; i++)
            {
                if (linebreaks[i] > startPosition)
                {
                    linebreaks[i] += relativeChange;
                }
            }
        }

        public void ScrollCursorIntoView(bool fullInval = false)
        {
            (int x, int y) currentCursorXY = XYfromBufferPosition(CursorPosition);
            int maxX = WindowWidth - 1;
            int maxY = WindowHeight - 1 - BottomMargin;
            int scrollX = 0;
            int scrollY = 0;
            if (currentCursorXY.x > maxX)
            {
                scrollX = currentCursorXY.x - maxX;
            } else if (currentCursorXY.x < 0)
            {
                scrollX = currentCursorXY.x;
            }
            if (currentCursorXY.y > maxY)
            {
                scrollY = currentCursorXY.y - maxY;
            } else if (currentCursorXY.y < 0)
            {
                scrollY = currentCursorXY.y;
            }
            if (fullInval)
            {
                ScrollX += scrollX;
                ScrollY += scrollY;
                InvalidateRegion(0, 0, WindowWidth, WindowHeight, true);
                PaintLineCounter();
            } else if (scrollY > 0 && CreepUp)
            {
                if (WindowHeight + scrollY >= Console.WindowHeight)
                {
                    if (WindowHeight < Console.WindowHeight - 1)
                    {
                        scrollY = (Console.WindowHeight - 1) - WindowHeight;
                        Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
                        for (int i = 0; i < scrollY; i++)
                        {
                            Console.WriteLine();
                        }
                        WindowY -= scrollY;
                        WindowHeight += scrollY;
                        ScrollY += scrollY;
                        ScrollCursorIntoView(true);
                        return;
                    } else
                    {
                        ScrollRelativeXY(scrollX, scrollY);
                        return;
                    }
                }
                Console.SetCursorPosition(0, Console.WindowTop + Console.WindowHeight - 1);
                for (int i = 0; i < scrollY; i++)
                {
                    Console.WriteLine();
                }
                WindowY -= scrollY;
                WindowHeight += scrollY;
                //ScrollY += scrollY;
                InvalidateRegion(0, WindowHeight - scrollY, WindowWidth, scrollY);
                PaintLineCounter(WindowHeight - scrollY);
            } else if (scrollX != 0 || scrollY != 0)
            {
                ScrollRelativeXY(scrollX, scrollY);
            }
        }

        public (int x, int y) XYfromBufferPosition(int bufferPosition)
        {
            int linebreakIndex = linebreaks.FindLastIndex((int linebreak) => { return linebreak < bufferPosition; });
            int y;
            int x;
            if (linebreakIndex > -1)
            {
                y = linebreakIndex + 1;
                x = (bufferPosition - (linebreaks[linebreakIndex])) - 1;
            }
            else
            {
                y = 0;
                x = bufferPosition;
            }
            return (x - ScrollX, y - ScrollY);
        }
        public int BufferPositionFromXY(int x, int y)
        {
            x += ScrollX;
            y += ScrollY;
            if (y >= linebreaks.Count + 1)
            {
                return -1;
            }
            int bufferPos;
            if (y == 0)
            {
                if (x < lineMaxLength(0))
                {
                    bufferPos = x;
                } else
                {
                    return -1;
                }
            } else
            {
                if (x < lineMaxLength(y))
                {
                    bufferPos = linebreaks[y - 1] + 1 + x;
                } else
                {
                    return -1;
                }
            }
            if (bufferPos <= Buffer.Length)
            {
                return bufferPos;
            } else
            {
                return -1;
            }
        }
        public void UpdateTip()
        {
            if (!TipSource.HasContent)
            {
                if (lastTipHeight > -1)
                {
                    InvalidateRegion(lastTipX - ScrollX, lastTipY - ScrollY, lastTipWidth, lastTipHeight, true);
                    lastTipHeight = -1;
                }
            } else
            {
                (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                int newTipWidth = Math.Min(maxTipWidth, Math.Max(TipSource.Annotation.Length, tipModelLength()));
                int newTipX = Math.Max(cursorScreenPos.x + ScrollX - (newTipWidth / 2), ScrollX);
                int newTipY = cursorScreenPos.y + ScrollY + 1;
                int newTipHeight = 1;
                string[] newTipAnnotationLines = null;
                if (TipSource.Annotation.Length > 0)
                {
                    newTipAnnotationLines = DrawingHelpers.WrapText(TipSource.Annotation, newTipWidth);
                    newTipHeight += newTipAnnotationLines.Length;
                }
                int tipScreenX = newTipX - ScrollX;
                int tipScreenY = newTipY - ScrollY;
                if (lastTipHeight > -1)
                {
                    InvalidateRegion(lastTipX - ScrollX, lastTipY - ScrollY, lastTipWidth, lastTipHeight, true, (int x, int y) =>
                    {
                        return !(x >= tipScreenX && y >= tipScreenY && x < tipScreenX + newTipWidth && y < tipScreenY + newTipHeight);
                    });
                }
                lastTipX = newTipX;
                lastTipY = newTipY;
                lastTipWidth = newTipWidth;
                lastTipHeight = newTipHeight;
                tipAnnotationLines = newTipAnnotationLines;
                paintTip();
            }
        }
        public void UpdateAutoComplete()
        {
            AutocompleteOption[]? sourceResult = null;
            if (AutocompleteSource != null)
            {
                sourceResult = AutocompleteSource.ToArray();
            }
            if (sourceResult == null)
            {
                if (lastAutocompleteHeight > -1)
                {
                    Func<int, int, bool>? filter = null;
                    if (TipSource.HasContent)
                    {
                        filter = (int x, int y) =>
                        {
                            return !(x >= lastTipX - ScrollX && y >= lastTipY - ScrollY && x < lastTipX - ScrollX + lastTipWidth && y < lastTipY - ScrollY + lastTipHeight);
                        };
                    }
                    InvalidateRegion(lastAutocompleteX - ScrollX, lastAutocompleteY - ScrollY, autocompleteWidth, lastAutocompleteHeight, true, filter);
                    lastAutocompleteHeight = -1;
                }
            } else
            {
                (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                int newAutocompleteX = Math.Max(cursorScreenPos.x + ScrollX - autocompleteWidth, ScrollX);
                int newAutocompleteY = cursorScreenPos.y + ScrollY + 1;
                if (TipSource.HasContent)
                {
                    newAutocompleteY += lastTipHeight;
                }
                int autocompleteScreenX = newAutocompleteX - ScrollX;
                int autocompleteScreenY = newAutocompleteY - ScrollY;
                int newAutocompleteHeight = Math.Min(Math.Min(maxAutocompleteHeight, sourceResult.Length), WindowHeight - autocompleteScreenY);
                if (lastAutocompleteHeight > -1)
                {
                    InvalidateRegion(lastAutocompleteX - ScrollX, lastAutocompleteY - ScrollY, autocompleteWidth, lastAutocompleteHeight, true, (int x, int y) =>
                    {
                        if (x >= autocompleteScreenX && y >= autocompleteScreenY && x < autocompleteScreenX + autocompleteWidth && y < autocompleteScreenY + newAutocompleteHeight)
                        {
                            return false;
                        } else if (TipSource.HasContent && x >= lastTipX - ScrollX && y >= lastTipY - ScrollY && x < lastTipX - ScrollX + lastTipWidth && y < lastTipY - ScrollY + lastTipHeight)
                        {
                            return false;
                        } else
                        {
                            return true;
                        }
                    });
                }
                lastAutocompleteX = newAutocompleteX;
                lastAutocompleteY = newAutocompleteY;
                lastAutocompleteHeight = newAutocompleteHeight;
                lastAutocompleteOptions = sourceResult;
                autocompleteScroll = 0;
                autocompleteSelectedItem = 0;
                paintAutocomplete(0, newAutocompleteHeight);
            }
        }
        private int tipModelLength()
        {
            int totalLength = 2 + TipSource.Keyword.Length;
            foreach (string arg in TipSource.Args)
            {
                totalLength += arg.Length + 1;
            }
            return totalLength;
        }
        private void paintTip()
        {
            int tipScreenX = lastTipX - ScrollX;
            int tipScreenY = lastTipY - ScrollY;
            int drawnTipWidth = Math.Min(lastTipWidth, WindowWidth - tipScreenX);
            int drawnTipHeight = Math.Min(lastTipHeight, WindowHeight - tipScreenY);
            Console.BackgroundColor = ConsoleColor.White;
            for (int y = tipScreenY; y < tipScreenY + drawnTipHeight; y++)
            {
                Console.SetCursorPosition(WindowX + tipScreenX, WindowY + y);
                if (y == tipScreenY)
                {
                    int totalLength = tipModelLength();
                    int charsWritten = 0;
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write("(");
                    charsWritten++;
                    string keyword = TipSource.Keyword;
                    if (keyword.Length > drawnTipWidth - charsWritten)
                    {
                        keyword = keyword.Substring(0, drawnTipWidth - charsWritten);
                    }
                    Console.ForegroundColor = TipSource.KeywordColor;
                    Console.Write(keyword);
                    charsWritten += keyword.Length;
                    int startArg = 0;
                    if (totalLength > drawnTipWidth)
                    {
                        if (TipSource.HighlightedArg > -1)
                            startArg = TipSource.HighlightedArg;
                        string ellipseText = " ...";
                        if (startArg > 0 && charsWritten + ellipseText.Length < drawnTipWidth)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                            Console.Write(ellipseText);
                            charsWritten += ellipseText.Length;
                        }
                    }
                    for (int i = startArg; charsWritten < drawnTipWidth && i < TipSource.Args.Length; i++)
                    {
                        string argText = " " + TipSource.Args[i];
                        if (argText.Length > drawnTipWidth - charsWritten)
                        {
                            int charsAvailable = drawnTipWidth - charsWritten;
                            if (charsAvailable >= 3)
                            {
                                argText = keyword.Substring(0, charsAvailable - 3) + "...";
                            } else
                            {
                                argText = keyword.Substring(0, charsAvailable);
                            }
                        }
                        if (i == TipSource.HighlightedArg)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                        } else
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                        }
                        Console.Write(argText);
                        charsWritten += argText.Length;
                    }
                    if (charsWritten < drawnTipWidth)
                    {
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.Write(")" + new string(' ', drawnTipWidth - charsWritten - 1));
                    }
                } else
                {
                    int annotationLine = y - tipScreenY - 1;
                    string annotationText = "";
                    if (annotationLine < tipAnnotationLines.Length)
                    {
                        annotationText = tipAnnotationLines[annotationLine];
                    }
                    if (annotationText.Length > drawnTipWidth)
                    {
                        annotationText = annotationText.Substring(0, drawnTipWidth);
                    } else
                    {
                        annotationText = annotationText + new string(' ', drawnTipWidth - annotationText.Length);
                    }
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write(annotationText);
                }
            }
            Console.BackgroundColor = ConsoleColor.Black;
        }
        private void paintAutocomplete(int rowStart, int rowLength)
        {
            int autocompleteScreenX = lastAutocompleteX - ScrollX;
            int autocompleteScreenY = lastAutocompleteY - ScrollY;
            int drawnAutocompleteWidth = Math.Min(autocompleteWidth, WindowWidth - autocompleteScreenX);
            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;
            for (int row = rowStart; row < rowStart + rowLength; row++)
            {
                AutocompleteOption option = lastAutocompleteOptions[lastAutocompleteOptions.Length - 1 - row];
                string optionText = option.OptionText;
                if (optionText.Length > drawnAutocompleteWidth)
                {
                    optionText = optionText.Substring(0, drawnAutocompleteWidth);
                }
                string paintText = optionText + new string(' ', drawnAutocompleteWidth - optionText.Length);
                string endSymbol = null;
                if (row == autocompleteScroll && row > 0)
                {
                    paintText = paintText.Substring(0, drawnAutocompleteWidth - 1);
                    endSymbol = "^";
                } else if (row == autocompleteScroll + lastAutocompleteHeight - 1 && row + 1 < lastAutocompleteOptions.Length)
                {
                    paintText = paintText.Substring(0, drawnAutocompleteWidth - 1);
                    endSymbol = "v";
                }
                Console.SetCursorPosition(WindowX + autocompleteScreenX, WindowY + autocompleteScreenY + (row - autocompleteScroll));
                if (autocompleteSelectedItem == row)
                {
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.Write(paintText);
                    Console.BackgroundColor = ConsoleColor.Gray;
                } else
                {
                    Console.Write(paintText);
                }
                if (endSymbol != null)
                {
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write(endSymbol);
                    Console.BackgroundColor = ConsoleColor.Gray;
                    Console.ForegroundColor = ConsoleColor.Black;
                }
            }
            Console.BackgroundColor = ConsoleColor.Black;
        }
        private bool updateAutocompleteScroll()
        {
            if (autocompleteSelectedItem < autocompleteScroll)
            {
                autocompleteScroll -= autocompleteScroll - autocompleteSelectedItem;
            } else if (autocompleteSelectedItem >= autocompleteScroll + lastAutocompleteHeight)
            {
                autocompleteScroll += (lastAutocompleteHeight + 1) - (autocompleteSelectedItem - autocompleteScroll);
            } else
            {
                return false;
            }
            paintAutocomplete(autocompleteScroll, lastAutocompleteHeight);
            return true;
        }
        private bool autocompleteProc(ConsoleKeyInfo keyInfo, out bool cleanBreak)
        {
            if (lastAutocompleteOptions.Length == 0)
            {
                cleanBreak = false;
                return false;
            }
            cleanBreak = true;
            if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (autocompleteSelectedItem > 0)
                {
                    autocompleteSelectedItem--;
                    if (!updateAutocompleteScroll())
                    {
                        paintAutocomplete(autocompleteSelectedItem + 1, 1);
                        paintAutocomplete(autocompleteSelectedItem, 1);
                    }
                    return true;
                }
            } else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (autocompleteSelectedItem + 1 < lastAutocompleteOptions.Length)
                {
                    autocompleteSelectedItem++;
                    if (!updateAutocompleteScroll())
                    {
                        if (autocompleteSelectedItem - 1 >= 0)
                        {
                            paintAutocomplete(autocompleteSelectedItem - 1, 1);
                        }
                        paintAutocomplete(autocompleteSelectedItem, 1);
                    }
                    return true;
                }
            } else if (autocompleteSelectedItem > -1 && ((keyInfo.Key == ConsoleKey.Enter && keyInfo.Modifiers == 0) || keyInfo.Key == ConsoleKey.Tab))
            {
                AutocompleteOption chosenOption = lastAutocompleteOptions[lastAutocompleteOptions.Length - 1 - autocompleteSelectedItem];
                Buffer.Insert(CursorPosition, chosenOption.FullText);
                MoveLineBreaks(CursorPosition, chosenOption.FullText.Length);
                ProcessSourceChange();
                (int x, int y) cursorScreenPos = XYfromBufferPosition(CursorPosition);
                InvalidateRegion(cursorScreenPos.x, cursorScreenPos.y, WindowWidth - cursorScreenPos.x, 1);
                CursorPosition = CursorPosition + chosenOption.FullText.Length;
                ScrollCursorIntoView();
                return false;
            }
            cleanBreak = false;
            return false;
        }
        private int lineMaxLength(int line)
        {
            return GetLineBufferRange(line).length + 1;
        }
        public (int start, int length) GetLineBufferRange(int line)
        {
            int lineStart = 0;
            if (line > 0)
            {
                lineStart = linebreaks[line - 1] + 1;
            }
            if (line < linebreaks.Count)
            {
                return (lineStart, linebreaks[line] - lineStart - 1);
            } else
            {
                return (lineStart, Buffer.Length - lineStart);
            }
        }
        public int LineIndexFromBufferPosition(int bufferPosition)
        {
            int linebreakIndex = linebreaks.FindLastIndex((int linebreak) => { return linebreak < bufferPosition; });
            if (linebreakIndex > -1)
            {
                return linebreakIndex + 1;
            } else
            {
                return 0;
            }
        }
    }
    public class KeyBinding
    {
        public ConsoleKey Key { get; set; }
        public KeyBindingCallback Callback { get; set; }
        public KeyBinding(ConsoleKey key, KeyBindingCallback callback)
        {
            Key = key;
            Callback = callback;
        }
    }
    public struct AutocompleteOption
    {
        public string OptionText { get; set; }
        public string FullText { get; set; }
        public int Rank { get; set; }

        public AutocompleteOption(string optionText, string fullText, int rank)
        {
            OptionText = optionText;
            FullText = fullText;
            Rank = rank;
        }
    }
    public struct TipContent
    {
        public static TipContent NoContent { get; } = new TipContent();

        public bool HasContent { get; }
        public string Keyword { get; set; } = "";
        public ConsoleColor KeywordColor { get; set; } = ConsoleColor.Magenta;
        public string[] Args { get; set; } = null;
        public int HighlightedArg { get; set; } = 0;
        public string Annotation { get; set; } = "";

        public TipContent()
        {
            HasContent = false;
        }
        public TipContent(string keyword, string[] args, int highlightedArg, string annotation, ConsoleColor keywordColor = ConsoleColor.Magenta)
        {
            HasContent = true;
            Keyword = keyword;
            KeywordColor = keywordColor;
            Args = args;
            HighlightedArg = highlightedArg;
            Annotation = annotation;
        }
    }
    public delegate bool KeyBindingCallback(ConsoleLispEditor editor, ConsoleKeyInfo keyInfo);
}

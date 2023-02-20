using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace CLI_Sample
{
    public partial class swCLIWindow : UserControl, IOutputWindow
    {
        public RichTextBox RTB => richTextBox1;
        public HashSet<Alias> RegisteredAliases { get; } = new();
        public IReadOnlyList<Command> RegisteredCommands { get => _commands.Values.Distinct().ToList(); }
        public Account? SelectedAccount { get; set; } = null;
        public Instrument? SelectedInstrument { get; set; } = null;
        private bool _isLongRunningTaskBusy { get; set; } = false;
        public bool CommandWaitingForUserInput { get; set; } = false;
        public bool CancelTaskTriggered { get; set; } = false;

        private Dictionary<string, Command> _commands = new();

        private int _caretPositionOnCommandLineRelativeToLine = 0;
        private int _caretPositionOnCommandLineRelativeToWindow = 0;

        private int _previousCommandIndex = -1;
        private List<string> _previousCommands = new();

        private string _commandPromptHeader
        {
            get
            {
                if (SelectedInstrument != null && SelectedAccount != null)
                    return $"[:]{SelectedAccount.Exchange}: {SelectedAccount.Name} - {SelectedInstrument.Name}[:]";
                else if (SelectedAccount != null)
                    return $"[:]{SelectedAccount.Exchange}: {SelectedAccount.Name}[:]";
                else
                    return "";
            }
        }

        private string _commandPrompt
        {
            get
            {
                if (SelectedAccount != null)
                    return $">";
                else
                    return "[:]>";
            }
        }

        public swCLIWindow()
        {
            InitializeComponent();
            InitializeCommands();
            richTextBox1.Text = _commandPrompt;
            richTextBox1.SelectionStart = richTextBox1.Text.Length;

#if (DEBUG)
            RegisteredAliases.Add(new Alias() { Key = "bd", Value = "buy bin1 btcusd 100 @best-1" });
            RegisteredAliases.Add(new Alias() { Key = "b1", Value = "buy bin1 btcusd_perp 100 @best-1" });
            RegisteredAliases.Add(new Alias() { Key = "b2", Value = "buy bin1 btcusd_perp 50.5 @99.54" });
            RegisteredAliases.Add(new Alias() { Key = "b3", Value = "buy bin1 ethusd 999.005 @0.0950" });
#endif

            UpdateTextBoxes();

            txtHelp.Text = GetHelpString(false);
        }

        private void InitializeCommands()
        {
            var commands = ReflectiveEnumerator.GetEnumerableOfType<Command>(this);
            foreach (var command in commands)
            {
                string commandName = command.ToString();
                if (_commands.ContainsKey(commandName))
                    throw new Exception($"Command initialization failed. Cannot add multiple commands with the same name: {commandName}");
                _commands.Add(commandName, command);
                /*foreach (var alias in command.CommandAliases)
                {
                    if (_commands.ContainsKey(alias))
                        throw new Exception($"Command initialization failed. Cannot add multiple commands with the same name/alias: {alias}");

                    _commands.Add(alias, command);
                }*/
            }
        }


        #region control events
        private void richTextBox1_SelectionChanged(object sender, EventArgs e)
        {
            if (IsLastLine())
            {
                int currentLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
                int lineStartIndex = richTextBox1.GetFirstCharIndexFromLine(currentLineIndex);
                if (richTextBox1.SelectionStart - lineStartIndex <= _commandPrompt.Length)
                {
                    int spaceToReduce = _commandPrompt.Length - (richTextBox1.SelectionStart - lineStartIndex);
                    richTextBox1.Select(lineStartIndex + _commandPrompt.Length, richTextBox1.SelectionLength - spaceToReduce);
                }
                _caretPositionOnCommandLineRelativeToLine = richTextBox1.SelectionStart - lineStartIndex;
                _caretPositionOnCommandLineRelativeToWindow = richTextBox1.SelectionStart;
            }
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            int cursorPosition = richTextBox1.SelectionStart;
            int currentLineIndex = richTextBox1.GetLineFromCharIndex(cursorPosition);
            int lastLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.Text.Length);
            string lineText = richTextBox1.Lines.Length > 0 ? richTextBox1.Lines[currentLineIndex] : "";

            string s = "";
            s += $"Lines.Count: {richTextBox1.Lines.Count()}\r\n";
            s += $"SelectionStart: {cursorPosition}\r\n";
            s += $"lineIndex: {currentLineIndex}\r\n";
            s += $"lineText: {lineText}\r\n";
            s += $"TextLength: {richTextBox1.Text.Length}\r\n";

            txtTextChanged.Text = s;

        }

        private async void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isLongRunningTaskBusy)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }
            if (CommandWaitingForUserInput)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                if (e.KeyValue == 32) //space
                    CommandWaitingForUserInput = false;

                if (e.KeyValue == 27) //esc
                {
                    CommandWaitingForUserInput = false;
                    CancelTaskTriggered = true;
                }

                if (ModifierKeys == Keys.Control && e.KeyValue == 67) //ctrl + c (cancel)
                {
                    CommandWaitingForUserInput = false;
                    CancelTaskTriggered = true;
                }

                return;
            }
            else if (e.KeyValue == 38) //up arrow
            {
                if (_previousCommandIndex == -1)
                    _previousCommandIndex = _previousCommands.Count;

                if (_previousCommandIndex > 0)
                {
                    SelectEditableText();
                    _previousCommandIndex--;
                    richTextBox1.SelectedText = _previousCommands[_previousCommandIndex];
                }
                e.Handled = true;
                return;
            }
            else if (e.KeyValue == 40) //down arrow
            {
                if (_previousCommandIndex != -1 && _previousCommandIndex < _previousCommands.Count - 1)
                {
                    SelectEditableText();
                    _previousCommandIndex++;
                    richTextBox1.SelectedText = _previousCommands[_previousCommandIndex];
                }
                e.Handled = true;
                return;
            }
            else if (e.KeyValue == 9) //tab
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                AutoComplete(richTextBox1.Lines.Last().ToLower());
            }
            else if (e.KeyValue == 8) //backspace
            {
                bool ignoreBackSpace = false;

                if (!IsLastLine())
                    ignoreBackSpace = true;
                else if (_caretPositionOnCommandLineRelativeToLine <= _commandPrompt.Length && richTextBox1.SelectedText.Length == 0)
                    ignoreBackSpace = true;

                if (ignoreBackSpace)
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (ModifierKeys == Keys.Control && (e.KeyValue == 67 || e.KeyValue == 88)) // 67 = c (copy) 87 = x (cut)
            {
                if (richTextBox1.SelectedText.Length > 0)
                {
                    Clipboard.SetText(richTextBox1.SelectedText);
                    if (e.KeyValue == 88 && IsLastLine())
                        richTextBox1.SelectedText = "";
                }
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }
            else if (ModifierKeys == Keys.Control && e.KeyValue == 86) // 86 =v (past)
            {
                string clipBoardText = Clipboard.GetText();
                if (clipBoardText.Contains("\n"))
                {
                    foreach (var line in clipBoardText.Split("\n"))
                    {
                        if (!string.IsNullOrEmpty(line.Trim()))
                        {
                            AppendText(line);
                            await RunCommand(line);
                        }
                    }
                    e.Handled = true;
                    return;
                }
            }
            else if ((e.KeyValue >= 48 && e.KeyValue <= 57) ||
                (e.KeyValue >= 65 && e.KeyValue <= 90) ||
                e.KeyValue == 32 ||
                e.KeyValue == 8 ||
                e.KeyValue == 46)
            {
                int cursorPosition = richTextBox1.SelectionStart;
                int currentLineIndex = richTextBox1.GetLineFromCharIndex(cursorPosition);
                int lastLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.Text.Length);

                if (currentLineIndex != lastLineIndex)
                {
                    richTextBox1.SelectionStart = _caretPositionOnCommandLineRelativeToWindow;
                    richTextBox1.SelectionLength = 0;
                }
            }
            else if (e.KeyValue == 13) //enter
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                await RunCommand(richTextBox1.Lines.Last().ToLower());
            }

            _previousCommandIndex = -1;
            txtKeyDown.Text = $"KeyCode: {e.KeyCode}\r\nKeyValue: {e.KeyValue}\r\nKeyData: {e.KeyData}\r\n{DateTime.Now:ffffff}";
        }

        private void txt_SizeChanged(object sender, EventArgs e)
        {
            if (sender is Control ctrl)
            {
                toolTip1.SetToolTip(ctrl, $"Width:{ctrl.Width}{Environment.NewLine}Height:{ctrl.Height}");
            }
        }
        #endregion

        #region commands
        private Command? _lastRunCommand = null;
        private async Task RunCommand(string cmd)
        {
            try
            {
                var text = cmd;
                if (text.StartsWith(_commandPrompt))
                    text = text.Remove(0, _commandPrompt.Length);

                text = text.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (!string.IsNullOrWhiteSpace(_commandPromptHeader))
                        richTextBox1.AppendText($"{Environment.NewLine}");
                    return;
                }

                richTextBox1.AppendText($"{Environment.NewLine}");

                _previousCommands.Add(text);
                _previousCommandIndex = -1;

                var itemsInCmd = Regex.Split(text, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                for (int i = 0; i < itemsInCmd.Count(); i++)
                {
                    itemsInCmd[i] = itemsInCmd[i].Trim('"');
                }

                //var itemsInCmd = Regex.Matches(text, @"[\""].+?[\""]|[^ ]+")
                //.Cast<Match>()
                //.Select(m => m.Value)
                //.ToArray();

                //var itemsInCmd = text.Split(' ');
                if (itemsInCmd[0]?.ToLower() != "unalias" && itemsInCmd[0]?.ToLower() != "alias")
                    itemsInCmd = ApplyAliases(itemsInCmd);

                bool runByUseStatement = false;
                var cmdText = itemsInCmd[0].ToLowerInvariant();

                if (cmdText == "use" && _lastRunCommand?.UseStatement != null)
                {
                    cmdText = _lastRunCommand?.UseStatement;
                    runByUseStatement = true;
                }

                if (!_commands.ContainsKey(cmdText))
                    throw new Exception($"No CommandName {cmdText}. Type help for available CommandNames");

                var commandToRun = _commands[cmdText];
                if (!runByUseStatement)
                    _lastRunCommand = commandToRun;
                await commandToRun.RunCommand(itemsInCmd);
            }
            catch (DuplicateResultException dex)
            {
                AppendText($"Error: {dex.Message}", new Font(this.Font, FontStyle.Bold), Color.Red);
                TableHelper.PrintTableOutput(this, dex.Duplicates);
            }
            catch (Exception ex)
            {
                AppendText($"Error: {ex.Message}", new Font(this.Font, FontStyle.Bold), Color.Red);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(richTextBox1.Text))
                    richTextBox1.AppendText($"{Environment.NewLine}");
                AppendComandPromptHeader();
                richTextBox1.AppendText($"{_commandPrompt}");
                UpdateTextBoxes();
            }
        }

        private string[] ApplyAliases(string[] itemsInCmd)
        {
            List<string> result = new();
            for (int i = 0; i < itemsInCmd.Length; i++)
            {
                if (RegisteredAliases.Any(x => x.Key == itemsInCmd[i]))
                {

                    var items = Regex.Split(RegisteredAliases.Single(x => x.Key == itemsInCmd[i]).Value, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");
                    result.AddRange(items);
                }
                else
                    result.Add(itemsInCmd[i]);
            }
            return result.ToArray();
        }
        #endregion

        #region text box helpers
        private void SelectEditableText()
        {
            var i = richTextBox1.Text.LastIndexOf($"\n");
            if (i == -1)
                return;
            richTextBox1.SelectionStart = i + _commandPrompt.Length;
            richTextBox1.SelectionLength = richTextBox1.TextLength - i + _commandPrompt.Length;
        }

        private void AppendComandPromptHeader()
        {
            if (SelectedInstrument != null && SelectedAccount != null)
            {
                AppendText("[:]", Color.Green, false);
                AppendText(SelectedAccount.Exchange.ToString(), Color.Red, false);
                AppendText(" - " + SelectedAccount.Name.ToString() + " - ", Color.Purple, false);
                AppendText(SelectedInstrument.Name.ToString(), Color.Orange, false);
                AppendText("[:]", Color.Green);
            }
            else if (SelectedAccount != null)
            {
                AppendText("[:]", Color.Green, false);
                AppendText(SelectedAccount.Exchange.ToString() + ": ", Color.Red, false);
                AppendText(SelectedAccount.Name.ToString(), Color.Purple, false);
                AppendText("[:]", Color.Green);
            }
        }
        public void ResetCLRText()
        {
            richTextBox1.ResetText();
        }
        public void ClearLastLine()
        {
            if (InvokeRequired)
            {
                Invoke(ClearLastLine);
                return;
            }
            var i = richTextBox1.Text.LastIndexOf($"\n");
            if (i == -1)
                return;
            richTextBox1.SelectionStart = i;
            richTextBox1.SelectionLength = richTextBox1.TextLength - i;
            richTextBox1.SelectedText = "";
            richTextBox1.AppendText(Environment.NewLine);
        }
        public void SelectLastLine()
        {
            if (InvokeRequired)
            {
                Invoke(SelectLastLine);
                return;
            }
            var i = richTextBox1.Text.LastIndexOf($"\n");
            if (i == -1)
                return;
            richTextBox1.SelectionStart = i;
            richTextBox1.SelectionLength = richTextBox1.TextLength - i;
        }
        private bool IsLastLine(int cursorPosition = -1)
        {
            if (cursorPosition == -1)
                cursorPosition = richTextBox1.SelectionStart;

            int currentLineIndex = richTextBox1.GetLineFromCharIndex(cursorPosition);
            int lastLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.Text.Length);
            return currentLineIndex == lastLineIndex;
        }

        public void AppendText(string text, bool autoReturn = true)
        {
            AppendText(text, null, null, autoReturn);
        }

        public void AppendText(string text, Color? textColor, bool autoReturn = true)
        {
            AppendText(text, null, textColor, autoReturn);
        }

        public void AppendText(string text, Font? font, bool autoReturn = true)
        {
            AppendText(text, font, null, autoReturn);
        }

        public void AppendText(string text, Font? font, Color? textColor, bool autoReturn = true)
        {
            if (InvokeRequired)
            {
                Invoke(() => { AppendText(text, font, textColor, autoReturn); });
                return;
            }

            var currentFont = richTextBox1.SelectionFont;
            var currentTextCol = richTextBox1.SelectionColor;
            if (font != null)
                richTextBox1.SelectionFont = font;
            if (textColor.HasValue)
                richTextBox1.SelectionColor = textColor.Value;
            richTextBox1.AppendText($"{text}");
            if (autoReturn)
                richTextBox1.AppendText($"{Environment.NewLine}");
            richTextBox1.SelectionFont = currentFont;
            richTextBox1.SelectionColor = currentTextCol;
        }
        #endregion

        public void StartLongRunningTask(string outputMessage, bool showLoadingSpinner)
        {
            _isLongRunningTaskBusy = true;
            Task.Run(() =>
            {
                AppendText($"{outputMessage} / ", false);

                int pos = 0;
                Action a = new Action(() =>
                {
                    ++pos;
                    SelectLastLine();
                    switch (pos)
                    {
                        case 1:
                            richTextBox1.SelectedText = @$"{Environment.NewLine}{outputMessage} /";
                            break;
                        case 2:
                            richTextBox1.SelectedText = @$"{Environment.NewLine}{outputMessage} -";
                            break;
                        case 3:
                            richTextBox1.SelectedText = @$"{Environment.NewLine}{outputMessage} \";
                            break;
                        case 4:
                            richTextBox1.SelectedText = @$"{Environment.NewLine}{outputMessage} |";
                            pos = 0;
                            break;
                        default:
                            break;
                    }
                });


                do
                {
                    if (InvokeRequired)
                        Invoke(a);
                    else
                        a();
                    
                    Thread.Sleep(250);
                } while (_isLongRunningTaskBusy);
            });


        }

        public void StopLongRunningTask()
        {
            _isLongRunningTaskBusy = false;
        }

        public void AutoComplete(string cmd)
        {
            try
            {
                var text = cmd;
                if (text.StartsWith(_commandPrompt))
                    text = text.Remove(0, _commandPrompt.Length);

                if (string.IsNullOrWhiteSpace(text))
                    return;

                var itemsInCmd = Regex.Split(text, "(?<=^[^\"]*(?:\"[^\"]*\"[^\"]*)*) (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

                List<string> matchingOptions = new();

                if (itemsInCmd.Length == 1)
                    matchingOptions = _commands.Where(x => x.Key.StartsWith(text)).Select(x => x.Key).ToList();
                else
                    matchingOptions = _commands[itemsInCmd[0]].AutoComplete(itemsInCmd);

                if (matchingOptions.Count == 0)
                    return;
                else if (matchingOptions.Count() == 1 && matchingOptions.Single() == itemsInCmd.Last())
                    return;
                else if (matchingOptions.Count() == 1)
                {
                    richTextBox1.SelectionStart = richTextBox1.TextLength - itemsInCmd.Last().Length;
                    richTextBox1.SelectionLength = itemsInCmd.Last().Length;
                    string autoComplete = matchingOptions.Single();
                    if (autoComplete.Contains(' '))
                        autoComplete = $"\"{matchingOptions.Single()}\"";
                    richTextBox1.SelectedText = autoComplete;
                    //AppendText(matchingOptions.Single().Remove(0, itemsInCmd.Last().Length), false);
                }
                else
                {
                    var repeatingCharacters = GetRepeatingCharacters(matchingOptions);
                    var charsToAdd = repeatingCharacters.Remove(0, itemsInCmd.Last().Length);
                    if (charsToAdd == "")
                    {
                        int col = 0;
                        AppendText($"{Environment.NewLine}", false);
                        foreach (var item in matchingOptions)
                        {
                            bool autoReturn = col == 3 || matchingOptions.Last() == item;
                            AppendText($"{item,-25}", autoReturn);
                            if (autoReturn)
                                col = 0;
                            else
                                ++col;
                        }

                        AppendText($"{Environment.NewLine}{_commandPrompt}{text}", false);
                    }
                    else
                    {
                        AppendText(charsToAdd, false);
                    }
                }
            }
            catch (Exception ex)
            {
                /*AppendText($"Error: {ex.Message}", new Font(this.Font, FontStyle.Bold), Color.Red);
                AppendComandPromptHeader();
                richTextBox1.AppendText($"{_commandPrompt}");
                UpdateTextBoxes();*/
            }
        }

        private string GetRepeatingCharacters(IEnumerable<string> strings)
        {
            int matchingCharCount = 0;
            for (int i = 0; i < strings.Min(x => x.Length); i++)
            {
                bool match = true;
                char currentIndexChar = strings.First()[i];
                foreach (var item in strings)
                {
                    if (item[i] != currentIndexChar)
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    ++matchingCharCount;
                else
                    break;
            }
            if (matchingCharCount == -1)
                return "";
            else
                return strings.First().Substring(0, matchingCharCount);
        }

        public string GetHelpString(bool includeDescription)
        {
            string textHelp = $"{"Command",-15}";
            if (includeDescription)
                textHelp += $"{"Description",-70}";
            textHelp += $"{Environment.NewLine}-------------  ";
            if (includeDescription)
                textHelp += "----------------------------------------------------------------------";
            foreach (var item in RegisteredCommands)
            {
                textHelp += $"{Environment.NewLine}{item,-15}";
                if (includeDescription)
                    textHelp += $"{item.Desciption,-70}";
            }

            return textHelp;
        }

        private void UpdateTextBoxes()
        {
            string txtSelectionText = "";
            txtSelectionText += $"{"Account:",-12}{SelectedAccount?.Name ?? "none",-30}";

            txtSelectionText += Environment.NewLine;

            txtSelectionText += $"{"Instrument:",-12}{SelectedInstrument?.Symbol ?? "none",-30}";
            txtSelection.Text = txtSelectionText;

            txtAliases.Text = TableHelper.CreateTableOutput(RegisteredAliases.ToList());// GetAliasesString().Trim();
        }

        private void nudPageSize_ValueChanged(object sender, EventArgs e)
        {
            if (chkPageOutput.Checked)
                TableHelper.PageSize = (int)nudPageSize.Value;
            else
                TableHelper.PageSize = 0;
        }

        private void chkPageOutput_CheckedChanged(object sender, EventArgs e)
        {
            if (chkPageOutput.Checked)
                TableHelper.PageSize = (int)nudPageSize.Value;
            else
                TableHelper.PageSize = 0;
        }
    }

    public static class TableHelper
    {
        public static int PageSize { get; set; } = 0;
        public static Dictionary<Type, Dictionary<int, object>> OutputsByType = new();

        private const int ColumnSpacer = 2;
        private static Dictionary<Type, List<PropertyInfoCliAttributeMapper>> _knownTypes = new();
        public static string CreateTableOutput<T>(List<T> collection, bool addRowId = false)
        {
            Type type = collection.GetType().GetGenericArguments()[0];
            ClearPreviousTablePrint(type);
            List<TableColumnHelper> columns = GetColumns(collection);
            string table = CreateTableHeadersText(addRowId, columns);

            //Data
            int rowId = 1;
            foreach (var item in collection)
            {
                if (addRowId)
                {
                    OutputsByType[typeof(T)].Add(rowId, item);
                    table += string.Format("{0," + (7 * -1).ToString() + "}", rowId.ToString());
                }

                foreach (var column in columns)
                {
                    string cellValue = GetCellValue(item, column);
                    table += cellValue;
                }
                table += Environment.NewLine;
                ++rowId;
            }

            return table;
        }

        public static async Task PrintTableOutput(IOutputWindow outputWindow, ICollection collection, bool addRowId = false)
        {
            Type type = collection.GetType().GetGenericArguments()[0];
            ClearPreviousTablePrint(type);
            List<TableColumnHelper> columns = GetColumns(collection);
            outputWindow.AppendText(CreateTableHeadersText(addRowId, columns), false);
            //Data
            int rowId = 1;
            string tableContent = string.Empty;
            foreach (var item in collection)
            {
                if (addRowId)
                {
                    OutputsByType[type].Add(rowId, item);
                    tableContent += string.Format("{0," + (7 * -1).ToString() + "}", rowId.ToString());
                }

                foreach (var column in columns)
                {
                    string cellValue = GetCellValue(item, column);
                    tableContent += string.Format("{0," + ((column.ColumnWidth + ColumnSpacer) * -1).ToString() + "}", cellValue);
                }
                tableContent += Environment.NewLine;

                if (PageSize > 0 && (double)rowId % (PageSize) == 0)
                {
                    outputWindow.AppendText(tableContent, false);
                    tableContent = string.Empty;
                    outputWindow.AppendText("--More-- (space = continue, ctrl + c = cancel)", false);
                    outputWindow.CommandWaitingForUserInput = true;
                    do
                    {
                        await Task.Run(() => { Thread.Sleep(100); });
                    } while (outputWindow.CommandWaitingForUserInput && !outputWindow.CancelTaskTriggered);

                    if (outputWindow.CancelTaskTriggered)
                    {
                        outputWindow.CommandWaitingForUserInput = false;
                        outputWindow.CancelTaskTriggered = false;
                        outputWindow.AppendText("");
                        return;
                    }

                    outputWindow.ClearLastLine();
                }

                ++rowId;
            }
            outputWindow.AppendText(tableContent);
        }

        private static void ClearPreviousTablePrint(Type type)
        {
            if (!OutputsByType.ContainsKey(type))
                OutputsByType[type] = new Dictionary<int, object>();

            OutputsByType[type].Clear();
        }

        private static string CreateTableHeadersText(bool addRowId, List<TableColumnHelper> columns)
        {
            string table = string.Empty;
            //Headers
            if (addRowId)
                table += string.Format("{0," + (7 * -1).ToString() + "}", "RowId");
            foreach (var column in columns)
            {
                var l = string.Format("{0," + ((column.ColumnWidth + ColumnSpacer) * -1).ToString() + "}", column.ColumnHeader);
                table += l;
            }
            table += Environment.NewLine;

            //Header break
            if (addRowId)
                table += new string('-', 5) + new string(' ', 2);
            foreach (var column in columns)
            {

                table += new string('-', column.ColumnWidth) + new string(' ', ColumnSpacer);
            }
            table += Environment.NewLine;
            return table;
        }

        private static string GetCellValue<T>(T? item, TableColumnHelper column)
        {
            string value = column.PropertyInfo.GetValue(item)?.ToString() ?? "";
            bool padLeft = true;
            if (column.DecimalWidth > 0 && (column.PropertyInfo.PropertyType == typeof(decimal) || column.PropertyInfo.PropertyType == typeof(double) || column.PropertyInfo.PropertyType == typeof(float)))
            {
                var sections = value.Split('.');
                if (sections.Length == 2)
                    value = sections[0] + "." + sections[1].Trim().TrimEnd('0').PadRight(column.DecimalWidth);
                else
                    value = value + "".PadRight(column.DecimalWidth + 1); //Add 1 for the decimal place
                padLeft = false;
            }

            string frontValue = "";
            string endValue = "";
            
            if (column.Format.LeadingCharacters != -1)
                frontValue = value.Remove(column.Format.TrailingCharacters);
            if (column.Format.TrailingCharacters != -1)
                endValue = value.Remove(0, value.Length - column.Format.TrailingCharacters);

            if (frontValue != "" || endValue != "")
                value = $"{frontValue}...{endValue}";

            if (value.Length > column.ColumnWidth)
                value = value.Remove(column.ColumnWidth - 3) + "...";

            if (padLeft)
                value = string.Format("{0," + ((column.ColumnWidth + ColumnSpacer) * -1).ToString() + "}", value);
            else
                value = string.Format("{0," + column.ColumnWidth.ToString() + "}", value);
            return value;
        }

        private static List<TableColumnHelper> GetColumns(ICollection collection)
        {
            Type type = collection.GetType().GetGenericArguments()[0];
            var props = AddToKnownTypes(type);
            List<TableColumnHelper> columns = new();
            foreach (var prop in props)
            {
                if (prop.CliTableFormat.HideFromOutput)
                    continue;

                int colWidth = 0;

                int maxWholeNumberLength = 0;
                int maxDecimalLength = 0;

                if (prop.PropertyInfo.PropertyType == typeof(decimal) || prop.PropertyInfo.PropertyType == typeof(double) || prop.PropertyInfo.PropertyType == typeof(float))
                {
                    foreach (var item in collection)
                    {
                        int wLen = 0;
                        int dLen = 0;

                        string strValue = prop.PropertyInfo.GetValue(item)?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(strValue))
                            continue;

                        var pieces = strValue.Split('.');
                        wLen = pieces[0].Length;
                        if (pieces.Length > 1)
                            dLen = pieces[1].Trim().TrimEnd('0').Length;


                        if (wLen > maxWholeNumberLength)
                            maxWholeNumberLength = wLen;
                        if (dLen > maxDecimalLength)
                            maxDecimalLength = dLen;
                    }
                    colWidth = maxWholeNumberLength + maxDecimalLength + 1; //add 1 for the decimal point
                }
                else if (collection.Count > 0)
                {
                    foreach (var item in collection)
                    {
                        int width = prop.PropertyInfo.GetValue(item)?.ToString()?.Length ?? 0;
                        if (colWidth < width)
                            colWidth = width;
                    }
                }

                string header = prop.CliTableFormat.Header ?? prop.PropertyInfo.Name;

                if (colWidth < header.Length)
                    colWidth = header.Length;

                if (prop.CliTableFormat.MaxWidth != -1 && colWidth > prop.CliTableFormat.MaxWidth)
                {
                    colWidth = prop.CliTableFormat.MaxWidth;
                    maxDecimalLength = colWidth - maxWholeNumberLength;
                }

                TableColumnHelper col = new()
                {
                    ColumnHeader = header,
                    ColumnWidth = colWidth,
                    DecimalWidth = maxDecimalLength,
                    PropertyInfo = prop.PropertyInfo,
                    Format = prop.CliTableFormat,
                };
                columns.Add(col);
            }

            return columns;
        }

        private static List<PropertyInfoCliAttributeMapper> AddToKnownTypes(Type colType)
        {
            if (!_knownTypes.ContainsKey(colType))
                _knownTypes[colType] = GetProperties(colType);

            return _knownTypes[colType];
        }

        private static List<PropertyInfoCliAttributeMapper> GetProperties(Type type)
        {
            List<PropertyInfoCliAttributeMapper> res = new();
            foreach (var prop in type.GetProperties())
            {
                var att = prop.GetCustomAttribute<CliTablePropertyFormat>();
                if (att == null)
                    att = new CliTablePropertyFormat();

                res.Add(new() { PropertyInfo = prop, CliTableFormat = att });
            }
            return res;
        }

        public class TableColumnHelper
        {
            public string ColumnHeader { get; set; }
            public int ColumnWidth { get; init; }
            //Only used for decimal/double/float
            public int DecimalWidth { get; init; }
            public PropertyInfo PropertyInfo { get; set; }
            public CliTablePropertyFormat Format { get; set; }
        }

        public class PropertyInfoCliAttributeMapper
        {
            public PropertyInfo PropertyInfo { get; set; }
            public CliTablePropertyFormat CliTableFormat { get; set; }
        }
    }

    public class CliTablePropertyFormat: Attribute
    {
        public bool HideFromOutput { get; set; }
        public string? Header { get; set; } = null;
        public int TrailingCharacters { get; set; } = -1;
        public int LeadingCharacters { get; set; } = -1;
        public int MaxWidth { get; set; } = 50;
    }

    public class Alias
    {
        [CliTablePropertyFormat(Header="Alias")]
        public string Key { get; set; }
        [CliTablePropertyFormat(Header = "Replacement value")]
        public string Value { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is Alias alias2))
                return false;

            if (ReferenceEquals(this, obj)) 
                return true;

            if (Key == alias2.Key)
                return true;

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }


    public interface IOutputWindow
    {
        Account? SelectedAccount { get; set; }
        Instrument? SelectedInstrument { get; set; }

        IReadOnlyList<Command> RegisteredCommands { get; }

        HashSet<Alias> RegisteredAliases { get; }
        bool CommandWaitingForUserInput { get; set; }
        bool CancelTaskTriggered { get; set; }
        
        void AppendText(string text, bool autoReturn = true);
        void AppendText(string text, Color? textColor, bool autoReturn = true);
        void AppendText(string text, Font? font, bool autoReturn = true);
        void AppendText(string text, Font? font, Color? textColor, bool autoReturn = true);
        void ResetCLRText();
        void ClearLastLine();

        void StartLongRunningTask(string outputMessage, bool showLoadingSpinner);
        void StopLongRunningTask();
    }

    public enum CommandName
    {
        none,
        help,
        clear,

        accounts,
        account,
        instruments,
        instrument,

        bal,

        orders,
        buy,
        sell,
        cancel,

        aliases,
        alias,
        unalias,
        info
    }

    public abstract class Command : IComparable<Command>
    {
        private List<string> HelpSwitches => new() { "help", "/?", "-help", "--help", "-?", "--?", "-H", "--H", "-h", "--h" };

        protected IOutputWindow _outputWindow;
        public Command(IOutputWindow outputWindow)
        {
            _outputWindow = outputWindow;
            CommandAliases.Add(CMDName.ToString());
        }
        [CliTablePropertyFormat(Header = "Command")]
        public abstract CommandName CMDName { get; }
        [CliTablePropertyFormat(HideFromOutput = true)]
        public virtual string UseStatement { get; }
        public async Task RunCommand(string[] cmdItems)
        {
            if (cmdItems.Length == 2 && HelpSwitches.Contains(cmdItems[1]) && !HelpSwitches.Contains(cmdItems[0]))
                _outputWindow.AppendText(CommandSyntaxMessage);
            else
            {
                var result = CommandAction(cmdItems);
                await result;
            }
        }
        protected void RaiseSyntaxException(string usageMessage)
        {
            throw new Exception($"Incorrect syntax. Expected syntax:{Environment.NewLine}\t{usageMessage}");
        }
        public abstract string Desciption { get; }
        [CliTablePropertyFormat(Header = "Syntax", MaxWidth = -1)]
        public abstract string CommandSyntaxMessage { get; }
        [CliTablePropertyFormat(HideFromOutput = true)]
        public abstract string CommandSyntaxDetails { get; }
        [CliTablePropertyFormat(HideFromOutput = true)]
        public abstract string CommandExample { get; }
        [CliTablePropertyFormat(HideFromOutput = true)]
        public virtual List<string> CommandAliases { get; } = new();

        [CliTablePropertyFormat(HideFromOutput = true)]
        internal virtual List<string> AutoComplete(string[] itemsInCmd)
        {
            return new();
        }

        [CliTablePropertyFormat(HideFromOutput = true)]
        protected abstract Func<string[], Task> CommandAction { get; }

        public int CompareTo(Command? other)
        {
            return CMDName.CompareTo(other?.CMDName);
        }

        public override string ToString()
        {
            return CMDName.ToString();
        }
    }

    public class HelpCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.help;

        public HelpCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Print help to the command window";
        public override string CommandSyntaxMessage => $"{CMDName} [ command ]";
        public override string CommandSyntaxDetails => $"{"command", 15} :: name of a command to get further details on";
        public override string CommandExample => $"{CMDName,15} :: prints general help information"
            +$"{Environment.NewLine}{$"{CMDName} buy",15} :: prints detailed information about the buy command";

        public override List<string> CommandAliases { get; } = new() { "/?", "-help", "-?", "-H", "-h" };

        internal override List<string> AutoComplete(string[] itemsInCmd)
        {
            if (itemsInCmd.Length == 2)
                return _outputWindow.RegisteredCommands.Where(x => x.ToString().StartsWith(itemsInCmd[1])).Select(x=>x.ToString()).ToList();
            else 
                return new();
        }

        protected override Func<string[], Task> CommandAction => PrintHelp;
        
        private async Task PrintHelp(string[] cmd)
        {
            if (cmd.Length == 2 && _outputWindow.RegisteredCommands.Count(x => x.CommandAliases.Contains(cmd[1])) == 1)
            {
                var command = _outputWindow.RegisteredCommands.Single(x => x.CommandAliases.Contains(cmd[1]));
                string output = $"Description:{Environment.NewLine}\t{command.Desciption}";
                output += $"{Environment.NewLine}Syntax:{Environment.NewLine}\t{command.CommandSyntaxMessage}";
                if (!string.IsNullOrWhiteSpace(command.CommandSyntaxDetails))
                    output += $"{Environment.NewLine}Parameters:{Environment.NewLine}{command.CommandSyntaxDetails}";
                output += $"{Environment.NewLine}Example:{Environment.NewLine}{command.CommandExample}";
                _outputWindow.AppendText(output);// outputWindow.GetHelpString(true));
            }
            else
                await TableHelper.PrintTableOutput(_outputWindow, _outputWindow.RegisteredCommands.ToList());
                //_outputWindow.AppendText(_outputWindow.GetHelpString(true));
        }
    }

    public class ClearCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.clear;

        public ClearCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Clear the command window";
        public override string CommandSyntaxMessage => $"{CMDName}";
        public override string CommandSyntaxDetails => "";
        public override string CommandExample => $"{CMDName,15} :: clears the command window"
            +$"{Environment.NewLine}{"clr",15} :: a short-hand that clears the command window";

        public override List<string> CommandAliases { get; } = new() { "clr" };

        protected override Func<string[], Task> CommandAction => ClearOutput;

        private async Task ClearOutput(string[] cmd)
        {
            _outputWindow.ResetCLRText();
        }
    }

    public class AccountCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.account;

        public AccountCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Select and account to trade on";
        public override string CommandSyntaxMessage => $"{CMDName}( rowID | accountId | accountName )";
        public override string CommandSyntaxDetails => $"{"accountId",15} :: the guid linked to the account. This will be tested first" +
            $"{Environment.NewLine}{"accountName",15} :: the name of the account. Only valid if there are no duplicates";
        public override string CommandExample => $"{$"{CMDName} myAcc1",15} :: Marks myAcc1 as the selected account"
            + $"{Environment.NewLine}{"acc myAcc2",15} :: Marks myAcc2 as the selected account"
            + $"{Environment.NewLine}{"acc 2",15} :: If the accounts command has been run, account on row 2 of that output will be marked as the selected account";

        public override List<string> CommandAliases { get; } = new() { "acc" };

        internal override List<string> AutoComplete(string[] itemsInCmd)
        {
            if (itemsInCmd.Length == 2)
                return SampleData.FindAccounts(FilterType.StartsWith, itemsInCmd[1]).Select(x=> x.Name).ToList();
            else
                return new();
        }

        protected override Func<string[], Task> CommandAction => SelectAccount;

        private async Task SelectAccount(string[] cmd)
        {
            if (cmd.Length != 2) 
                RaiseSyntaxException(CommandSyntaxMessage);

            string accSearch = cmd[1];
            if (TableHelper.OutputsByType.ContainsKey(typeof(Account))
                    && int.TryParse(cmd[1], out int rowId)
                    && TableHelper.OutputsByType[typeof(Account)][rowId] is Account account)
            {
                accSearch = account.Id.ToString();
            }

            _outputWindow.SelectedAccount = SampleData.FindAccount(accSearch);
            _outputWindow.SelectedInstrument = null;
        }
    }

    public class InstrumentCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.instrument;

        public InstrumentCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Select an instrument to trade";
        public override string CommandSyntaxMessage => $"{CMDName} ( rowID | instrumentSymbol )";
        public override string CommandSyntaxDetails => $"{"instrumentSymbol",27} :: the exchange symbol of the instrument";
        public override string CommandExample => $"{$"{CMDName} BTCUSD",27} :: marks BTCUSD on the selected instrument's exchange as the selected instrument"
            + $"{Environment.NewLine}{$"{CMDName} myAcc1 ETHUSD",27} :: marks myAcc1 as the selected instrument and ETHUSD on the same exchange as the selected instrument"
            + $"{Environment.NewLine}{"inst myAcc2 BTCUSDC",27} :: marks myAcc2 as the selected instrument and BTCUSDC on the same exchange as the selected instrument"
            + $"{Environment.NewLine}{"inst 3",27} :: If the instruments command has been run, instrument on row 3 of that output will be marked as the selected instrument";

        public override List<string> CommandAliases { get; } = new() { "inst" };

        internal override List<string> AutoComplete(string[] itemsInCmd)
        {
            if (itemsInCmd.Length == 2)
            {
                if (_outputWindow.SelectedAccount != null)
                    return SampleData.FindInstruments(FilterType.StartsWith, _outputWindow.SelectedAccount, itemsInCmd[1]).Select(x => x.Symbol).ToList();
                else 
                    return SampleData.FindAccounts(FilterType.StartsWith, itemsInCmd[1]).Select(x => x.Name).ToList();
            }
            else if (itemsInCmd.Length == 3 && SampleData.TryFindAccount(itemsInCmd[1], out Account acc))
                return SampleData.FindInstruments(FilterType.StartsWith, acc, itemsInCmd[2]).Select(x => x.Symbol).ToList();
            else 
                return new();
        }

        protected override Func<string[], Task> CommandAction => SelectInstrument;

        private async Task SelectInstrument(string[] cmd)
        {
            Account? accToSelect = null;
            
            int instrumentIndex = 1;

            if (cmd.Length == 2)
            {
                accToSelect = _outputWindow.SelectedAccount;

                if (TableHelper.OutputsByType.ContainsKey(typeof(Instrument)) 
                    && int.TryParse(cmd[1], out int rowId)
                    && TableHelper.OutputsByType[typeof(Instrument)][rowId] is Instrument ins
                    && accToSelect?.Exchange == ins.Exchange)
                {
                    _outputWindow.SelectedInstrument = SampleData.FindInstrument(accToSelect, ins.SW_Symbol);
                    return;
                }
            }
            else if (cmd.Length == 3)
            {
                accToSelect = SampleData.FindAccount(cmd[1]);
                instrumentIndex = 2;
            }
            else if (cmd.Length != 2)
                RaiseSyntaxException(CommandSyntaxMessage);


            if (accToSelect == null)
                throw new Exception("Cannot select instrument before selecting account. Use 'account' command");

            var instrument = SampleData.FindInstrument(accToSelect, cmd[instrumentIndex]);

            if (accToSelect != _outputWindow.SelectedAccount)
                _outputWindow.SelectedAccount = accToSelect;

            _outputWindow.SelectedInstrument = instrument;
        }
    }

    public class AccountsCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.accounts;
        public override string? UseStatement => "account";
        public AccountsCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "List the loaded accounts";
        public override string CommandSyntaxMessage => $"{CMDName}";
        public override string CommandSyntaxDetails => "";
        public override string CommandExample => $"{CMDName,15} :: Lists all the loaded accounts";

        protected override Func<string[], Task> CommandAction => ListAccounts;

        private async Task ListAccounts(string[] cmd)
        {
            if (cmd.Length != 1)
                RaiseSyntaxException(CommandSyntaxMessage);

            bool showTip = true;

            await TableHelper.PrintTableOutput(_outputWindow, SampleData.Accounts, true);

            if (showTip)
                _outputWindow.AppendText("Tip: type 'use' followed by either the RowID or the account name to select an account");
        }
    }

    public class InstrumentsCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.instruments;
        public override string? UseStatement => "instrument";

        public InstrumentsCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "List instruments on the exchange of a given account";
        public override string CommandSyntaxMessage => $"{CMDName} [account | instumentFilter]";
        public override string CommandSyntaxDetails => $"{"account",15} :: specifies the account for which to list instruments"
            +$"{"instumentFilter",15} :: filters the result set. Can use * as wildcard at start or end";
        public override string CommandExample => $"{CMDName,20} :: Lists all the instruments on the exchange of the selected account"
            + $"{Environment.NewLine}{$"{CMDName} myAcc1",20} :: Lists all the instruments available on the exchange of myAcc1"
            + $"{Environment.NewLine}{$"{CMDName} *USD",20} :: Lists all the instruments on the exchange of the selected account who's symbols end with USD"
            + $"{Environment.NewLine}{$"{CMDName} BTC*",20} :: Lists all the instruments on the exchange of the selected account who's symbols starts with BTC";

        protected override Func<string[], Task> CommandAction => ListInsruments;

        private async Task ListInsruments(string[] cmd)
        {
            Account? acc = null;
            string filter = "";
            bool showTip = false;
            if (cmd.Length == 1)
            {
                if (_outputWindow.SelectedAccount == null)
                    throw new Exception("Cannot list instruments before selecting account. Use 'account' command");

                acc = _outputWindow.SelectedAccount;
                showTip = true;
            }
            else if (cmd.Length == 2)
            {
                if (SampleData.TryFindAccount(cmd[1], out acc))
                    acc = acc;
                else if (_outputWindow.SelectedAccount == null)
                    throw new Exception("Cannot list instruments before selecting account. Use 'account' command");
                else
                {
                    acc = _outputWindow.SelectedAccount;
                    filter = cmd[1];
                    if (!filter.StartsWith("*") && !filter.EndsWith("*"))
                        RaiseSyntaxException(CommandSyntaxMessage);
                }
            }
            else
                RaiseSyntaxException(CommandSyntaxMessage);

            FilterType filterType = FilterType.Equals;

            if (filter.StartsWith('*') && filter.EndsWith("*"))
                filterType = FilterType.Contains;
            else if (filter.StartsWith('*'))
                filterType = FilterType.EndsWith;
            else if (filter.EndsWith("*"))
                filterType = FilterType.StartsWith;
            else if (filter == "")
                filterType = FilterType.None;

            filter = filter.Replace("*", "");

            await TableHelper.PrintTableOutput(_outputWindow, SampleData.FindInstruments(filterType, _outputWindow.SelectedAccount, filter), true);

            if (showTip)
                _outputWindow.AppendText("Tip: type 'use' followed by either the RowID or the Symbol to select an instrument");
        }
    }

    public enum OrdersFilter
    {
        Open,
        Completed,
        All,
        State,
        Account,
        Instrument,
    }

    public abstract class OrderCommand : Command
    {
        public OrderCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => $"Places a {CMDName} order or {CMDName}s at market";
        public override string CommandSyntaxMessage => $"{CMDName} [ account ] [ instrument ] ( quantity ) [ price ]";
        public override string CommandSyntaxDetails => $"{"account",35} :: Optional. If not present the selected account will be used" +
            $"{Environment.NewLine}{"instrument",35} :: Optional. If not present the selected account will be used" + 
            $"{Environment.NewLine}{"quantity",35} :: specified as a decimal" +
            $"{Environment.NewLine}{"price",35} :: Optional. specified as a decimal. If not present market order will be placed";
        public override string CommandExample => $"{$"{CMDName} myAcc1 BTCUSD 100 @best-2",35} :: Places a limit order for 100 BTCUSD 2 ticks below best on myAcc1"
            + $"{Environment.NewLine}{$"{CMDName} ETHUSDC 50 @best+3",35} :: Places a limit order for 50 ETHUSDC 3 ticks above best on the selected account"
            + $"{Environment.NewLine}{$"{CMDName} 25 @95.55",35} :: Places a limit order for 25 of the selected instrument at a price of 95.55 (in quote currency) on the selected account"
            + $"{Environment.NewLine}{$"{CMDName} 10",35} :: Places a market order for 10 of the selected instrument on the selected account";

        internal override List<string> AutoComplete(string[] itemsInCmd)
        {
            if (itemsInCmd.Length == 2)
            {
                var result = SampleData.FindAccounts(FilterType.StartsWith, itemsInCmd[1]).Select(x => x.Name).ToList();
                if (_outputWindow.SelectedAccount != null)
                    result.AddRange(SampleData.FindInstruments(FilterType.StartsWith, _outputWindow.SelectedAccount, itemsInCmd[1]).Select(x => x.Symbol).ToList());
                return result;
            }
            else if (itemsInCmd.Length == 3 && SampleData.TryFindAccount(itemsInCmd[1], out Account acc))
            {
                return SampleData.FindInstruments(FilterType.StartsWith, acc, itemsInCmd[2]).Select(x => x.Symbol).ToList();
            }
            else
                return new();
        }


        protected async Task BuySell(string[] cmd)
        {
            Account? account = null;
            Instrument? instrument = null;
            decimal qty = 0;
            decimal price = 0;

            if (cmd.Length < 2)
                throw new Exception($"Too few arguments passed to {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
            //buy/sell qty (market)
            else if (cmd.Length == 2)
            {
                if (_outputWindow.SelectedAccount == null)
                    throw new Exception($"Must select an account or pass an account into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                if (_outputWindow.SelectedInstrument == null)
                    throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");

                account = _outputWindow.SelectedAccount;
                instrument = _outputWindow.SelectedInstrument;

                if (!decimal.TryParse(cmd[1], out qty))
                    throw new Exception($"Cannot convert {cmd[1]} to quantity");

                price = 999.12345m;
            }
            //buy/sell qty price
            //buy/sell instrument qty (market)
            else if (cmd.Length == 3)
            {
                if (_outputWindow.SelectedAccount == null)
                    throw new Exception($"Must select an account or pass an account into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                account = _outputWindow.SelectedAccount;

                //buy/sell instrument qty (market)
                if (SampleData.TryFindInstrument(account, cmd[1], out instrument))// .Instruments.Count(x => x.Exchange == account?.Exchange && x.Symbol == cmd[1]) == 1)
                {
                    if (!decimal.TryParse(cmd[2], out qty))
                        throw new Exception($"Cannot convert {cmd[2]} to quantity");
                    price = 999.12345m;
                }
                //buy/sell qty price
                else
                {
                    if (_outputWindow.SelectedInstrument == null)
                        throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                    instrument = _outputWindow.SelectedInstrument;

                    if (!decimal.TryParse(cmd[1], out qty))
                        throw new Exception($"Cannot convert {cmd[1]} to quantity");

                    price = ExtractPrice(cmd[2]);
                }
            }
            //buy/sell instrument qty price
            //buy/sell account instrument qty (market)
            else if (cmd.Length == 4)
            {
                //buy/sell account instrument qty (market)
                if (SampleData.TryFindAccount(cmd[1], out account))
                {
                    if (!SampleData.TryFindInstrument(account, cmd[2], out instrument))
                        throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");

                    if (!decimal.TryParse(cmd[3], out qty))
                        throw new Exception($"Cannot convert {cmd[3]} to quantity");

                    price = 999.12345m;
                }
                //buy/sell instrument qty price
                else
                {
                    if (_outputWindow.SelectedAccount == null)
                        throw new Exception($"Must select an account or pass an account into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                    account = _outputWindow.SelectedAccount;
                    if (!SampleData.TryFindInstrument(account, cmd[1], out instrument))
                        throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");

                    if (!decimal.TryParse(cmd[2], out qty))
                        throw new Exception($"Cannot convert {cmd[2]} to quantity");

                    price = ExtractPrice(cmd[3]);
                }
            }
            //buy/sell account instrument qty price
            else if (cmd.Length == 5)
            {
                account = SampleData.FindAccount(cmd[1]);

                if (!SampleData.TryFindInstrument(account, cmd[2], out instrument))
                    throw new Exception($"Cannot find {cmd[2]} in available instruments.{Environment.NewLine}{CommandSyntaxMessage}");

                if (!decimal.TryParse(cmd[3], out qty))
                    throw new Exception($"Cannot convert {cmd[3]} to quantity");

                price = ExtractPrice(cmd[4]);
            }

            if (account == null || instrument == null || qty == 0 || price == 0)
                throw new Exception($"Incorrect syntax.{Environment.NewLine}{CommandSyntaxMessage}");

            string outputPrice = price.ToString();
            if (price == 999.12345m)
                outputPrice = "market";
            else if (price == 55.12345m)
                outputPrice = "best";
            else if (price.ToString().StartsWith("55.12345"))
            {
                var val = int.Parse(price.ToString().Replace("55.12345", ""));
                outputPrice = "best" + (val - 5).ToString();
            }

            _outputWindow.StartLongRunningTask("Placing order", true);

            await SampleData.AddOrder(new()
            {
                Account = account,
                Instrument = instrument,
                Qty = qty,
                Price = price,
                Side = CMDName == CommandName.sell ? Side.Sell : Side.Buy,
                State = price == 999 ? OrderState.Filled : OrderState.Open,
            });

            _outputWindow.StopLongRunningTask();
            _outputWindow.ClearLastLine();

            _outputWindow.AppendText($"{CMDName} {qty} {instrument.Symbol} on account {account.Name} at {outputPrice}");
        }

        private static decimal ExtractPrice(string priceVal)
        {
            decimal price;
            if (priceVal.Replace("@", "") == "best")
                price = 55.12345m;
            else if (priceVal.Replace("@", "").StartsWith("best+") && int.TryParse(priceVal.Replace("@", "").Replace("best+", ""), out int xp))
            {
                if (xp > 4)
                    throw new Exception("This is only a sample. Please use numbers between 1 and 4 for best+ X or best- X. Be gentle friend");
                price = 55.12345m + ((5 + xp) / 1000000m);
            }
            else if (priceVal.Replace("@", "").StartsWith("best-") && int.TryParse(priceVal.Replace("@", "").Replace("best-", ""), out int xm))
            {
                if (xm > 4)
                    throw new Exception("This is only a sample. Please use numbers between 1 and 4 for best+ X or best- X. Be gentle friend");
                price = 55.12345m + ((5 - xm) / 1000000m);
            }
            else if (!decimal.TryParse(priceVal.Replace("@", ""), out price))
                throw new Exception($"Cannot convert {priceVal} to price");
            return price;
        }
    }

    public class BuyCommand : OrderCommand
    {
        public override CommandName CMDName { get; } = CommandName.buy;
        public BuyCommand(IOutputWindow outputWindow) : base(outputWindow) { }
        protected override Func<string[], Task> CommandAction => BuySell;
    }

    public class SellCommand : OrderCommand
    {
        public override CommandName CMDName { get; } = CommandName.sell;
        public SellCommand(IOutputWindow outputWindow) : base(outputWindow) { }
        protected override Func<string[], Task> CommandAction => BuySell;
    }

    public class OrdersCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.orders;

        public OrdersCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Lists all open orders (or use switches to list all orders)";
        public override string CommandSyntaxMessage => $"{CMDName} [ filter ]";
        public override string CommandSyntaxDetails => $"{"filter",15} :: Optional. [ open (default) | all | state | accountId | instrumentSymbol ]"
            + $"{Environment.NewLine}{"",15} :: if there is an account with the same name as an instrument's symbole, the filter will be applied on the account";
        public override string CommandExample => $"{CMDName,15} :: Lists all open orders on all loaded accounts"
            + $"{Environment.NewLine}{$"{CMDName} all",15} :: Lists all orders on all loaded accounts"
            + $"{Environment.NewLine}{$"{CMDName} myAcc1",15} :: Lists all orders on myAcc1"
            + $"{Environment.NewLine}{$"{CMDName} *USD",15} :: Lists all orders for symbole ending in USD";

        protected override Func<string[], Task> CommandAction => ListOrders;

        protected async Task ListOrders(string[] cmd)
        {
            OrdersFilter filterType = OrdersFilter.Open;
            string filter = "";
            if (cmd.Length == 1)
                filterType = OrdersFilter.Open;
            else if (cmd.Length == 2)
            {
                filter = cmd[1];
                if (cmd[1] == "open")
                    filterType = OrdersFilter.Open;
                else if (cmd[1] == "all")
                    filterType = OrdersFilter.All;
                else if (cmd[1] == "completed")
                    filterType = OrdersFilter.Completed;
                else if (Enum.TryParse(cmd[1], out OrderState x))
                    filterType = OrdersFilter.State;
                else if (SampleData.Accounts.Any(x => x.Name == cmd[1]))
                    filterType = OrdersFilter.Account;
                else if (SampleData.Instruments.Any(x => x.Symbol == cmd[1]))
                    filterType = OrdersFilter.Instrument;
                else
                    RaiseSyntaxException(CommandSyntaxMessage);
            }
            else
                RaiseSyntaxException(CommandSyntaxMessage);

            var orders = SampleData.FindOrders(filterType, filter);

            bool showTip = true;

            await TableHelper.PrintTableOutput(_outputWindow, orders, true);

            if (showTip)
                _outputWindow.AppendText("Tip: type 'cancel' followed by either the RowID or the OrderID to cancel an order");
        }
    }

    public class CancelCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.cancel;

        public CancelCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Cancels an order (or all orders using all switch)";
        public override string CommandSyntaxMessage => $"{CMDName} (rowID | orderId | all)";
        public override string CommandSyntaxDetails => $"{"orderId",15} :: The guid associated with an order or the word all";
        public override string CommandExample => $"{$"{CMDName} 884b5e",15} :: Cancels the order with OrderID 884b5e"
            + $"{Environment.NewLine}{$"{CMDName} all",15} :: Cancels all open orders"
            + $"{Environment.NewLine}{$"{CMDName} 4",15} :: If the orders command has been run, order on row 4 of that output will be canceled";

        protected override Func<string[], Task> CommandAction => CancelOrder;

        protected async Task CancelOrder(string[] cmd)
        {
            Guid orderIdToCancel = Guid.Empty;
            if (cmd.Length != 2)
                throw new Exception(CommandSyntaxMessage);

            if (cmd[1] == "all")
            {
                int affectedOrders = await SampleData.CancelOrders(SampleData.Orders);

                if (affectedOrders > 0)
                    _outputWindow.AppendText($"{affectedOrders} orders cancelled");
                else
                    _outputWindow.AppendText($"There are no open orders to cancel");

                return;
            }
            else if (TableHelper.OutputsByType.ContainsKey(typeof(Order))
                    && int.TryParse(cmd[1], out int rowId)
                    && TableHelper.OutputsByType[typeof(Order)][rowId] is Order order)
            {
                orderIdToCancel = order.ID;
            }

            if (orderIdToCancel == Guid.Empty && !Guid.TryParse(cmd[1], out orderIdToCancel))
                throw new Exception($"Cannot convert {cmd[1]} to order ID");


            var orders = SampleData.Orders.Where(x => x.ID == orderIdToCancel);
            if (orders.Count() == 0)
                throw new Exception($"No order with ID {orderIdToCancel}");
            else if (orders.Count() > 1)
                throw new Exception($"More than 1 matching order with ID {orderIdToCancel}");

            if (orders.Single().IsOpen)
            {
                await SampleData.CancelOrders(orders);
                _outputWindow.AppendText($"Order cancelled");
            }
            else
                _outputWindow.AppendText($"Cannot cancel an order that is not open.");
        }

    }

    public class AliasesCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.aliases;

        public AliasesCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Lists all the known aliases";
        public override string CommandSyntaxMessage => $"{CMDName} ";
        public override string CommandSyntaxDetails => "";
        public override string CommandExample => $"{CMDName,15} :: Lists all the aliases the user has registered";

        protected override Func<string[], Task> CommandAction => GetAliasesString;

        private async Task GetAliasesString(string[] cmd)
        {
            await TableHelper.PrintTableOutput(_outputWindow, _outputWindow.RegisteredAliases.ToList());
        }
    }

    public class AliasCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.alias;

        public AliasCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Creates an alias for for some part of a commonly used command";
        public override string CommandSyntaxMessage => $"{CMDName} ( key ) ( replacementValue )";
        public override string CommandSyntaxDetails => $"{"key",35} :: The key that will be used to alias a command. Maximum 8 characters" +
            $"{Environment.NewLine}\t{"replacementValue",35} :: Full or part of text that can be used to make up a valid command";
        public override string CommandExample => $"{$"{CMDName} ba1 buy myAcc1 BTCUSD 100",35} :: Adds an alias with the key ba1 which can be used as follows"
            + $"{Environment.NewLine}{"",35} :: ba1 - results in <buy myAcc1 BTCUSD 100> and will place a market order"
            + $"{Environment.NewLine}{"",35} :: ba1 @25000.95 - results in <buy myAcc1 BTCUSD 100 @25000.95> and will place a limit order";

        protected override Func<string[], Task> CommandAction => CreateAlias;

        private async Task CreateAlias(string[] cmd)
        {
            if (cmd.Length < 3)
                RaiseSyntaxException(CommandSyntaxMessage);

            string alias = cmd[1];

            if (alias.Length > 8)
                throw new Exception($"Alias cannot be more than 8 characters{Environment.NewLine}{CommandSyntaxMessage}");

            var x = cmd.Take(new Range(new Index(2), new Index(0, true)));

            var val = string.Join(" ", x);

            if (_outputWindow.RegisteredAliases.Any(x=>x.Key == alias))
                throw new Exception($"Alias {alias} already exists");

            if (Enum.TryParse(alias, out CommandName command))
                throw new Exception($"cannot use reserved word {alias} as an alias");

            if (_outputWindow.RegisteredAliases.Add(new() { Key = alias, Value = val }))
                _outputWindow.AppendText($"Alias {alias} added with value {val}");
            else
                throw new Exception($"Alias {alias} could not be registered");
        }
    }

    public class UnaliasCommand : Command
    {
        public override CommandName CMDName => CommandName.unalias;

        public UnaliasCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Deletes an alias";
        public override string CommandSyntaxMessage => $"{CMDName} ( key )";
        public override string CommandSyntaxDetails => $"{"key",15} :: The key that represents the alias to be removed";
        public override string CommandExample => $"{$"{CMDName} ba1",15} :: Deregisters a previously registered alias with the key ba1";

        protected override Func<string[], Task> CommandAction => RemoveAlias;

        private async Task RemoveAlias(string[] cmd)
        {
            if (cmd.Length != 2)
                RaiseSyntaxException(CommandSyntaxMessage);

            string alias = cmd[1];

            if (!_outputWindow.RegisteredAliases.Any(x=>x.Key == alias))
                throw new Exception($"Alias {alias} does not exist exists");

            _outputWindow.RegisteredAliases.RemoveWhere(x=>x.Key == alias);
        }
    }

    public class InfoCommand : Command
    {
        public override CommandName CMDName => CommandName.info;

        public InfoCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Prints some basic info about ";
        public override string CommandSyntaxMessage => $"{CMDName} [ instruments ]";
        public override string CommandSyntaxDetails => $"{"instruments",15} :: Optional. Pipe delimited list of instruments (can include account) to show values for. If not present the selected instrument will be used";
        public override string CommandExample => $"{CMDName,15} :: Will output basic info about the selected insturment" 
            + $"{CMDName + " BTCUSD",15} :: Will output basic info about BTCUSD on the exchange of the selected account"
            + $"{CMDName + " BTCUSD|ETHUSDC",15} :: Will output basic info about BTCUSD and ETHUSDC on the exchange of the selected account"
            + $"{CMDName + " myAcc1-BTCUSD|ETHUSDC",15} :: Will output basic info about BTCUSD on the excahnge of myAcc1 and ETHUSDC on the exchange of the selected account";

        protected override Func<string[], Task> CommandAction => Info;
        private async Task Info(string[] cmd)
        {
            if (cmd.Length > 2) //if the user has entered something like info BTCUSD | ETHUSD we want to change it to BTCUSD|ETHUSD
            {
                cmd[1] = string.Join("", cmd.Skip(1));
                cmd = cmd.Take(2).ToArray();
            }

            if (cmd.Length == 1)
            {
                if (_outputWindow.SelectedInstrument == null)
                    RaiseSyntaxException(CommandSyntaxMessage);

                var info = SampleData.GetInfo(SampleData.FindInstrument(_outputWindow.SelectedAccount, _outputWindow.SelectedInstrument.Symbol));
                await TableHelper.PrintTableOutput(_outputWindow, new List<InstrumentInfo>() { info });
            }
            else if (cmd.Length == 2)
            {
                string[] instrumentSymbols = cmd[1].Split('|');
                List<InstrumentInfo> instInfo = new();
                foreach (var instrument in instrumentSymbols)
                {
                    string[] accInst = instrument.Split('-');
                    if (accInst.Length == 1)
                    {
                        if (_outputWindow.SelectedAccount == null)
                            RaiseSyntaxException(CommandSyntaxMessage);
                        
                        var acc = _outputWindow.SelectedAccount;
                        var inst = SampleData.FindInstrument(acc, accInst[0]);
                        instInfo.Add(SampleData.GetInfo(inst));
                    }
                    else if (accInst.Length == 2)
                    {
                        var acc = SampleData.FindAccount(accInst[0]);
                        var inst = SampleData.FindInstrument(acc, accInst[1]);
                        instInfo.Add(SampleData.GetInfo(inst));
                    }
                }

                await TableHelper.PrintTableOutput(_outputWindow, instInfo);
            }
            else
                RaiseSyntaxException(CommandSyntaxMessage);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using static System.Windows.Forms.Design.AxImporter;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace CLI_Sample
{
    public partial class swCLIWindow : UserControl, IOutputWindow
    {
        public HashSet<Alias> RegisteredAliases { get; } = new();
        public IReadOnlyList<Command> RegisteredCommands { get => _commands.Values.Distinct().ToList(); }
        public Account? SelectedAccount { get; set; } = null;
        public Instrument? SelectedInstrument { get; set; } = null;

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

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == 38) //up arrow
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
            else if (e.KeyValue == 9)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                AutoComplete(richTextBox1.Lines.Last());
                return;
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
                Clipboard.SetText(richTextBox1.SelectedText);
                if (e.KeyValue == 88 && IsLastLine())
                    richTextBox1.SelectedText = "";
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
                            RunCommand(line);
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
                RunCommand(richTextBox1.Lines.Last());
                e.Handled = true;
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
        private void RunCommand(string cmd)
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

                var itemsInCmd = text.Split(' ');
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
                commandToRun.RunCommand(itemsInCmd);
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
                if (RegisteredAliases.Any(x=>x.Key == itemsInCmd[i]))
                    result.AddRange(RegisteredAliases.Single(x=>x.Key==itemsInCmd[i]).Value.Split(" "));
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
        private void ClearLastLine()
        {
            var i = richTextBox1.Text.LastIndexOf($"\n");
            if (i == -1)
                return;
            richTextBox1.SelectionStart = i;
            richTextBox1.SelectionLength = richTextBox1.TextLength - i + 1;
            richTextBox1.SelectedText = "";
            richTextBox1.AppendText($"{Environment.NewLine}");
            richTextBox1.AppendText(_commandPrompt);
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

        public void AutoComplete(string cmd)
        {
            try
            {
                var text = cmd;
                if (text.StartsWith(_commandPrompt))
                    text = text.Remove(0, _commandPrompt.Length);

                if (string.IsNullOrWhiteSpace(text))
                    return;

                var itemsInCmd = text.Split(' ');

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
                    AppendText(matchingOptions.Single().Remove(0, itemsInCmd.Last().Length), false);
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
                AppendText($"Error: {ex.Message}", new Font(this.Font, FontStyle.Bold), Color.Red);
                AppendComandPromptHeader();
                richTextBox1.AppendText($"{_commandPrompt}");
                UpdateTextBoxes();
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

            txtAliases.Text = FormatHelper.FormatTableOutput(RegisteredAliases);// GetAliasesString().Trim();
        }
    }

    public static class FormatHelper
    {
        private const int ColumnSpacer = 2;
        private static Dictionary<Type, List<PropertyInfoCliAttributeMapper>> _knownTypes = new();
        public static string FormatTableOutput<T>(IEnumerable<T> collection)
        {
            string table = string.Empty;
            List<TableColumnHelper> columns = GetColumns(collection);
            //Headers
            foreach (var column in columns)
            {
                var l = string.Format("{0," + ((column.ColumnWidth + ColumnSpacer) * -1).ToString() + "}", column.ColumnHeader);
                table += l;
            }
            table += Environment.NewLine;
            //Header break
            foreach (var column in columns)
            {
                table += new string('-', column.ColumnWidth) + new string(' ', ColumnSpacer);
            }
            table += Environment.NewLine;
            //Data
            foreach (var item in collection)
            {
                foreach (var column in columns)
                {
                    string cellValue = column.PropertyInfo.GetValue(item)?.ToString() ?? "";
                    if (cellValue.Length > column.ColumnWidth)
                    {
                        cellValue = cellValue.Remove(column.ColumnWidth - 3) + "...";
                    }
                    table += string.Format("{0," + ((column.ColumnWidth + ColumnSpacer) * -1).ToString() + "}", cellValue);
                }
                table += Environment.NewLine;
            }

            return table;
        }

        private static List<TableColumnHelper> GetColumns<T>(IEnumerable<T> collection)
        {
            var props = AddToKnownTypes<T>();
            List<TableColumnHelper> columns = new();
            foreach (var prop in props)
            {
                if (prop.CliTableFormat.HideFromOutput)
                    continue;

                int colWidth = 0;
                if (collection.Count() > 0)
                    colWidth = collection.Max(x => prop.PropertyInfo.GetValue(x)?.ToString()?.Length ?? 0);

                string header = prop.CliTableFormat.Header ?? prop.PropertyInfo.Name;

                if (colWidth < header.Length)
                    colWidth = header.Length;

                if (prop.CliTableFormat.MaxWidth != -1 && colWidth > prop.CliTableFormat.MaxWidth)
                    colWidth = prop.CliTableFormat.MaxWidth;

                TableColumnHelper col = new()
                {
                    ColumnHeader = header,
                    ColumnWidth = colWidth,
                    PropertyInfo = prop.PropertyInfo,
                };
                columns.Add(col);
            }

            return columns;
        }

        private static List<PropertyInfoCliAttributeMapper> AddToKnownTypes<T>()
        {
            Type colType = typeof(T);
            if (!_knownTypes.ContainsKey(colType))
                _knownTypes[colType] = GetProperties(colType);

            return _knownTypes[colType];
        }

        private static List<PropertyInfoCliAttributeMapper> GetProperties(Type type)
        {
            List<PropertyInfoCliAttributeMapper> res = new();
            foreach (var prop in type.GetProperties())
            {
                var att = prop.GetCustomAttribute<CliTableFormat>();
                if (att == null)
                    att = new CliTableFormat();

                res.Add(new() { PropertyInfo = prop, CliTableFormat = att });
            }
            return res;
        }

        public class TableColumnHelper
        {
            public string ColumnHeader { get; set; }
            public int ColumnWidth { get; init; }
            public PropertyInfo PropertyInfo { get; set; }
        }

        public class PropertyInfoCliAttributeMapper
        {
            public PropertyInfo PropertyInfo { get; set; }
            public CliTableFormat CliTableFormat { get; set; }
        }
    }

    public class CliTableFormat: Attribute
    {
        public bool HideFromOutput { get; set; }
        public string? Header { get; set; } = null;
        public int TrailingCharacters { get; set; } = -1;
        public int LeadingCharacters { get; set; } = -1;
        public int MaxWidth { get; set; } = 50;
    }

    public class Alias
    {
        [CliTableFormat(Header="Alias")]
        public string Key { get; set; }
        [CliTableFormat(Header = "Replacement value")]
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

        void AppendText(string text, bool autoReturn = true);
        void AppendText(string text, Color? textColor, bool autoReturn = true);
        void AppendText(string text, Font? font, bool autoReturn = true);
        void AppendText(string text, Font? font, Color? textColor, bool autoReturn = true);
        void ResetCLRText();
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
        unalias
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
        [CliTableFormat(Header = "Command")]
        public abstract CommandName CMDName { get; }
        [CliTableFormat(HideFromOutput = true)]
        public virtual string UseStatement { get; }
        public void RunCommand(string[] cmdItems)
        {
            if (cmdItems.Length == 2 && HelpSwitches.Contains(cmdItems[1]))
                _outputWindow.AppendText(CommandSyntaxMessage);
            else
                CommandAction(cmdItems);
        }
        protected void RaiseSyntaxException(string usageMessage)
        {
            throw new Exception($"Incorrect syntax. {usageMessage}");
        }
        public abstract string Desciption { get; }
        [CliTableFormat(Header = "Syntax", MaxWidth = -1)]
        public abstract string CommandSyntaxMessage { get; }
        [CliTableFormat(HideFromOutput = true)]
        public abstract string CommandSyntaxDetails { get; }
        [CliTableFormat(HideFromOutput = true)]
        public virtual List<string> CommandAliases { get; } = new();

        [CliTableFormat(HideFromOutput = true)]
        internal virtual List<string> AutoComplete(string[] itemsInCmd)
        {
            return new();
        }

        [CliTableFormat(HideFromOutput = true)]
        protected abstract Action<string[]> CommandAction { get; }

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
        public override string CommandSyntaxMessage => "help [ command ]";
        public override string CommandSyntaxDetails => $"{"command", 15} :: name of a command to get further details on";

        public override List<string> CommandAliases => new() { "/?", "-help", "-?", "-H", "-h" };

        internal override List<string> AutoComplete(string[] itemsInCmd)
        {
            if (itemsInCmd.Length == 2)
                return _outputWindow.RegisteredCommands.Where(x => x.ToString().StartsWith(itemsInCmd[1])).Select(x=>x.ToString()).ToList();
            else 
                return new();
        }

        protected override Action<string[]> CommandAction => PrintHelp;
        
        private void PrintHelp(string[] cmd)
        {
            if (cmd.Length == 2 && _outputWindow.RegisteredCommands.Count(x => x.CommandAliases.Contains(cmd[1])) == 1)
            {
                var command = _outputWindow.RegisteredCommands.Single(x => x.CommandAliases.Contains(cmd[1]));
                string output = command.Desciption;
                output += Environment.NewLine + command.CommandSyntaxMessage;
                output += Environment.NewLine + command.CommandSyntaxDetails;
                _outputWindow.AppendText(output);// outputWindow.GetHelpString(true));
            }
            else
                _outputWindow.AppendText(FormatHelper.FormatTableOutput(_outputWindow.RegisteredCommands));
                //_outputWindow.AppendText(_outputWindow.GetHelpString(true));
        }
    }

    public class ClearCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.clear;

        public ClearCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Clear the command window";
        public override string CommandSyntaxMessage => "clear";
        public override string CommandSyntaxDetails => $"{"aliases :: clr"}";

        public override List<string> CommandAliases => new() { "clr" };

        protected override Action<string[]> CommandAction => ClearOutput;

        private void ClearOutput(string[] cmd)
        {
            _outputWindow.ResetCLRText();
        }
    }

    public class AccountCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.account;

        public AccountCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Select and account to trade on";
        public override string CommandSyntaxMessage => "account ( accountId | accountName )";
        public override string CommandSyntaxDetails => $"{"accountId",15} :: the guid linked to the account. This will be tested first" +
            $"{Environment.NewLine}{"accountName",15} :: the name of the account. Only valid if there are no duplicates";

        public override List<string> CommandAliases => new() { "acc" };

        internal override List<string> AutoComplete(string[] itemsInCmd)
        {
            if (itemsInCmd.Length == 2)
                return SampleData.FindAccounts(FilterType.StartsWith, itemsInCmd[1]).Select(x=> x.Name).ToList();
            else
                return new();
        }

        protected override Action<string[]> CommandAction => SelectAccount;

        private void SelectAccount(string[] cmd)
        {
            if (cmd.Length != 2) 
                RaiseSyntaxException(CommandSyntaxMessage);
            
            _outputWindow.SelectedAccount = SampleData.FindAccount(cmd[1]);
            _outputWindow.SelectedInstrument = null;
        }
    }

    public class InstrumentCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.instrument;

        public InstrumentCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Select an instrument to trade";
        public override string CommandSyntaxMessage => "instrument ( instrumentSymbol )";
        public override string CommandSyntaxDetails => $"{"instrumentSymbol",15} :: the exchange symbol of the instrument";

        public override List<string> CommandAliases => new() { "inst" };

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

        protected override Action<string[]> CommandAction => SelectInstrument;

        private void SelectInstrument(string[] cmd)
        {
            Account? accToSelect = null;
            
            int instrumentIndex = 1;

            if (cmd.Length == 2)
                accToSelect = _outputWindow.SelectedAccount; 
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
        public override string CommandSyntaxMessage => "accounts";
        public override string CommandSyntaxDetails => "";

        protected override Action<string[]> CommandAction => ListAccounts;

        private void ListAccounts(string[] cmd)
        {
            if (cmd.Length != 1)
                RaiseSyntaxException(CommandSyntaxMessage);

            bool showTip = true;

            _outputWindow.AppendText(FormatHelper.FormatTableOutput(SampleData.Accounts));

            if (showTip)
                _outputWindow.AppendText("Tip: type 'use' followed by either the RowID or the account name to select an account");
        }
    }

    public class InstrumentsCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.instruments;
        public override string? UseStatement => "instrument";

        public InstrumentsCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "List the instruments available on a given accounts";
        public override string CommandSyntaxMessage => "instruments";
        public override string CommandSyntaxDetails => "";

        protected override Action<string[]> CommandAction => ListInsruments;

        private void ListInsruments(string[] cmd)
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

            /*_outputWindow.AppendText($"{Environment.NewLine}{"SYMBOL",-20}{"NAME",-25}");
            _outputWindow.AppendText($"-----------------   -----------------------");*/

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

            /*foreach (var item in SampleData.FindInstruments(filterType, _outputWindow.SelectedAccount, filter))
            {
                _outputWindow.AppendText($"{item.Symbol,-20}{item.Name,-25}");
            }*/


            _outputWindow.AppendText(FormatHelper.FormatTableOutput(SampleData.FindInstruments(filterType, _outputWindow.SelectedAccount, filter)));

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
        public override string CommandSyntaxDetails => $"{"account",15} :: Optional. If not present the selected account will be used" +
            $"{Environment.NewLine}{"instrument",15} :: Optional. If not present the selected account will be used" + 
            $"{Environment.NewLine}{"quantity",15} :: specified as a decimal" +
            $"{Environment.NewLine}{"price",15} :: Optional. specified as a decimal. If not present market order will be placed";

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

        protected override Action<string[]> CommandAction => BuySell;

        private void BuySell(string[] cmd)
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
            _outputWindow.AppendText($"{CMDName} {qty} {instrument.Symbol} on account {account.Name} at {outputPrice}");

            SampleData.Orders.Add(new()
            {
                Account = account,
                Instrument = instrument,
                Qty = qty,
                Price = price,
                Side = CMDName == CommandName.sell ? Side.Sell : Side.Buy,
                State = price == 999 ? OrderState.Filled : OrderState.Open,
            });
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
    }

    public class SellCommand : OrderCommand
    {
        public override CommandName CMDName { get; } = CommandName.sell;
        public SellCommand(IOutputWindow outputWindow) : base(outputWindow) { }
    }

    public class OrdersCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.orders;

        public OrdersCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Lists all open orders (or use switches to list all orders)";
        public override string CommandSyntaxMessage => "orders [ filter ]";
        public override string CommandSyntaxDetails => $"{"filter",15} :: Optional. [ open (default) | all | state | accountId | instrumentSymbol ] ";

        protected override Action<string[]> CommandAction => ListOrders;

        private void ListOrders(string[] cmd)
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

            _outputWindow.AppendText(FormatHelper.FormatTableOutput(orders));
        }
    }

    public class CancelCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.cancel;

        public CancelCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Cancels an order (or all orders using all switch)";
        public override string CommandSyntaxMessage => "cancel (orderId | all)";
        public override string CommandSyntaxDetails => $"{"orderId",15} :: The guid associated with an order or the word all";

        protected override Action<string[]> CommandAction => CancelOrder;

        private void CancelOrder(string[] cmd)
        {
            if (cmd.Length != 2)
                throw new Exception(CommandSyntaxMessage);

            if (cmd[1] == "all")
            {
                int affectedOrders = 0;
                foreach (var item in SampleData.Orders)
                {
                    if (item.IsOpen)
                    {
                        item.State = OrderState.Canceled;
                        ++affectedOrders;
                    }
                }
                if (affectedOrders > 0)
                    _outputWindow.AppendText($"{affectedOrders} orders cancelled");
                else
                    _outputWindow.AppendText($"There are no open orders to cancel");
                SampleData.Orders.ForEach(x => x.State = OrderState.Canceled);
                return;
            }

            if (!Guid.TryParse(cmd[1], out Guid orderId))
                throw new Exception($"Cannot convert {cmd[1]} to order ID");

            var order = SampleData.Orders.Where(x => x.ID == orderId);
            if (order.Count() == 0)
                throw new Exception($"No order with ID {orderId}");
            if (order.Count() > 1)
                throw new Exception($"More than 1 matching order with ID {orderId}");

            if (order.Single().IsOpen)
            {
                order.Single().State = OrderState.Canceled;
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
        public override string CommandSyntaxMessage => "aliases";
        public override string CommandSyntaxDetails => "";

        protected override Action<string[]> CommandAction => GetAliasesString;

        private void GetAliasesString(string[] cmd)
        {
            _outputWindow.AppendText(FormatHelper.FormatTableOutput(_outputWindow.RegisteredAliases));
        }
    }

    public class AliasCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.alias;

        public AliasCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Creates an alias for for some part of a commonly used command";
        public override string CommandSyntaxMessage => "alias ( key ) ( replacementValue )";
        public override string CommandSyntaxDetails => $"{"key",15} :: The key that will be used to alias a command. Maximum 8 characters" +
            $"{Environment.NewLine}\t{"replacementValue",15} :: Full or part of text that can be used to make up a valid command";

        protected override Action<string[]> CommandAction => CreateAlias;

        private void CreateAlias(string[] cmd)
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
        public override CommandName CMDName { get; } = CommandName.unalias;

        public UnaliasCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Deletes an alias";
        public override string CommandSyntaxMessage => "unalias ( key )";
        public override string CommandSyntaxDetails => $"{"key",15} :: The key that represents the alias to be removed";

        protected override Action<string[]> CommandAction => RemoveAlias;

        private void RemoveAlias(string[] cmd)
        {
            if (cmd.Length != 2)
                RaiseSyntaxException(CommandSyntaxMessage);

            string alias = cmd[1];

            if (!_outputWindow.RegisteredAliases.Any(x=>x.Key == alias))
                throw new Exception($"Alias {alias} does not exist exists");

            _outputWindow.RegisteredAliases.RemoveWhere(x=>x.Key == alias);
        }
    }
}

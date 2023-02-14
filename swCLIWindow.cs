using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CLI_Sample
{
    public partial class swCLIWindow : UserControl, IOutputWindow
    {
        public Dictionary<string, string> RegisteredAliases { get; } = new();
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
                if (itemsInCmd[0]?.ToLower() != "unalias")
                    itemsInCmd = ApplyAliases(itemsInCmd);
                
                var cmdText = itemsInCmd[0].ToLowerInvariant();

                if (cmdText == "use" && _lastRunCommand?.UseStatement != null)
                {
                    cmdText = _lastRunCommand?.UseStatement;
                }

                if (!_commands.ContainsKey(cmdText))
                    throw new Exception($"No CommandName {cmdText}. Type help for available CommandNames");

                var commandToRun = _commands[cmdText];
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
                if (RegisteredAliases.ContainsKey(itemsInCmd[i]))
                    result.AddRange(RegisteredAliases[itemsInCmd[i]].Split(" "));
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

        public string GetAliasesString()
        {
            string aliases = $"{Environment.NewLine}{"Alias",-12}{"Replacement value",-30}";
            aliases += $"{Environment.NewLine}----------  ------------------------------";
            foreach (var item in RegisteredAliases)
            {
                aliases += $"{Environment.NewLine}{item.Key,-12}{item.Value,-30}";
            }
            return aliases;
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

            txtAliases.Text = GetAliasesString().Trim();
        }
    }

    public interface IOutputWindow
    {
        Account? SelectedAccount { get; set; }
        Instrument? SelectedInstrument { get; set; }

        IReadOnlyList<Command> RegisteredCommands { get; }

        Dictionary<string, string> RegisteredAliases { get; }

        void AppendText(string text, bool autoReturn = true);
        void AppendText(string text, Color? textColor, bool autoReturn = true);
        void AppendText(string text, Font? font, bool autoReturn = true);
        void AppendText(string text, Font? font, Color? textColor, bool autoReturn = true);
        void ResetCLRText();

        string GetAliasesString();
        string GetHelpString(bool includeDescription);
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
        public abstract CommandName CMDName { get; }
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
        protected abstract Action<string[]> CommandAction { get; }
        public abstract string Desciption { get; }
        public abstract string CommandSyntaxMessage { get; }
        public abstract string CommandSyntaxDetails { get; }
        public virtual List<string> CommandAliases { get; } = new();

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

        protected override Action<string[]> CommandAction => PrintHelp;
        
        public override List<string> CommandAliases => new() { "/?", "-help", "-?", "-H", "-h" }; 
        
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
                _outputWindow.AppendText(_outputWindow.GetHelpString(true));
        }
    }

    public class ClearCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.clear;

        public ClearCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Clear the command window";
        public override string CommandSyntaxMessage => "clear";
        public override string CommandSyntaxDetails => $"{"aliases :: clr"}";

        protected override Action<string[]> CommandAction => ClearOutput;

        public override List<string> CommandAliases => new() { "clr" };


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

    protected override Action<string[]> CommandAction => SelectAccount;

        public override List<string> CommandAliases => new() { "acc" };

        private void SelectAccount(string[] cmd)
        {
            if (cmd.Length != 2) 
                RaiseSyntaxException(CommandSyntaxMessage);

            if (Guid.TryParse(cmd[1], out Guid id) && SampleData.Accounts.Count(x => x.Id == id) == 1)
            {
                _outputWindow.SelectedAccount = SampleData.Accounts.Single(x => x.Id == id);
                _outputWindow.SelectedInstrument = null;
                return;
            }
            else
            {
                var accountsMatchingName = SampleData.Accounts.Where(x => x.Name == cmd[1]);
                if (accountsMatchingName.Count() == 0)
                    throw new Exception($"No account with name {cmd[1]}");
                if (accountsMatchingName.Count() > 1)
                    throw new Exception($"More than 1 account with name {cmd[1]}. Please rename accounts");

                _outputWindow.SelectedAccount = accountsMatchingName.Single();
                _outputWindow.SelectedInstrument = null;
            }
        }
    }

    public class InstrumentCommand : Command
    {
        public override CommandName CMDName { get; } = CommandName.instrument;

        public InstrumentCommand(IOutputWindow outputWindow) : base(outputWindow) { }

        public override string Desciption => "Select an instrument to trade";
        public override string CommandSyntaxMessage => "instrument ( instrumentSymbol )";
        public override string CommandSyntaxDetails => $"{"instrumentSymbol",15} :: the exchange symbol of the instrument";

        protected override Action<string[]> CommandAction => SelectInstrument;

        public override List<string> CommandAliases => new() { "inst" };

        private void SelectInstrument(string[] cmd)
        {
            if (cmd.Length != 2)
                RaiseSyntaxException(CommandSyntaxMessage);

            if (_outputWindow.SelectedAccount == null)
                throw new Exception("Cannot select instrument before selecting account. Use 'account' command");

            var matchingInstruments = SampleData.Instruments.Where(x => x.Exchange == _outputWindow.SelectedAccount.Exchange && x.Symbol == cmd[1]);

            if (matchingInstruments.Count() == 0)
                throw new Exception($"No instrument with symbol {cmd[1]}");
            if (matchingInstruments.Count() > 1)
                throw new Exception($"More than 1 instrument with symbol {cmd[1]}. Please report this bug to the support team.");

            _outputWindow.SelectedInstrument = matchingInstruments.Single();
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

            foreach (var item in SampleData.Accounts)
            {
                _outputWindow.AppendText(item.Name);
            }
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
            if (cmd.Length != 1)
                RaiseSyntaxException(CommandSyntaxMessage);

            if (_outputWindow.SelectedAccount == null)
                throw new Exception("Cannot list instruments before selecting account. Use 'account' command");

            _outputWindow.AppendText($"{Environment.NewLine}{"SYMBOL",-20}{"NAME",-25}");
            _outputWindow.AppendText($"-----------------   -----------------------");
            foreach (var item in SampleData.Instruments.Where(x => x.Exchange == _outputWindow.SelectedAccount.Exchange))
            {
                _outputWindow.AppendText($"{item.Symbol,-20}{item.Name,-25}");
            }
        }
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

                price = 999;
            }
            //buy/sell qty price
            //buy/sell instrument qty (market)
            else if (cmd.Length == 3)
            {
                if (_outputWindow.SelectedAccount == null)
                    throw new Exception($"Must select an account or pass an account into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                account = _outputWindow.SelectedAccount;

                //buy/sell instrument qty (market)
                if (SampleData.Instruments.Count(x => x.Exchange == account?.Exchange && x.Symbol == cmd[1]) == 1)
                {
                    instrument = SampleData.Instruments.Single(x => x.Exchange == account?.Exchange && x.Symbol == cmd[1]);
                    if (!decimal.TryParse(cmd[2], out qty))
                        throw new Exception($"Cannot convert {cmd[2]} to quantity");
                    price = 999;
                }
                //buy/sell qty price
                else
                {
                    if (_outputWindow.SelectedInstrument == null)
                        throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                    instrument = _outputWindow.SelectedInstrument;

                    if (!decimal.TryParse(cmd[1], out qty))
                        throw new Exception($"Cannot convert {cmd[1]} to quantity");

                    if (!decimal.TryParse(cmd[2].Replace("@", ""), out price))
                        throw new Exception($"Cannot convert {cmd[2]} to price");
                }
            }
            //buy/sell instrument qty price
            //buy/sell account instrument qty (market)
            else if (cmd.Length == 4)
            {
                //buy/sell account instrument qty (market)
                if (SampleData.Accounts.Count(x => x.Name == cmd[1]) == 1)
                {
                    account = SampleData.Accounts.Single(x => x.Name == cmd[1]);

                    if (SampleData.Instruments.Count(x => x.Exchange == account?.Exchange && x.Symbol == cmd[2]) != 1)
                        throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                    instrument = SampleData.Instruments.Single(x => x.Exchange == account?.Exchange && x.Symbol == cmd[2]);

                    if (!decimal.TryParse(cmd[3], out qty))
                        throw new Exception($"Cannot convert {cmd[3]} to quantity");

                    price = 999;
                }
                //buy/sell instrument qty price
                else
                {
                    if (_outputWindow.SelectedAccount == null)
                        throw new Exception($"Must select an account or pass an account into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                    account = _outputWindow.SelectedAccount;

                    if (SampleData.Instruments.Count(x => x.Exchange == account?.Exchange && x.Symbol == cmd[1]) != 1)
                        throw new Exception($"Must select an instrument or pass an instrument into the {CMDName} command.{Environment.NewLine}{CommandSyntaxMessage}");
                    instrument = SampleData.Instruments.Single(x => x.Exchange == account?.Exchange && x.Symbol == cmd[1]);

                    if (!decimal.TryParse(cmd[2], out qty))
                        throw new Exception($"Cannot convert {cmd[2]} to quantity");

                    if (!decimal.TryParse(cmd[3].Replace("@", ""), out price))
                        throw new Exception($"Cannot convert {cmd[3]} to price");

                }
            }
            //buy/sell account instrument qty price
            else if (cmd.Length == 5)
            {
                if (SampleData.Accounts.Count(x => x.Name == cmd[1]) != 1)
                    throw new Exception($"Cannot find {cmd[1]} in loaded accounts.{Environment.NewLine}{CommandSyntaxMessage}");
                account = SampleData.Accounts.Single(x => x.Name == cmd[1]);

                if (SampleData.Instruments.Count(x => x.Exchange == account?.Exchange && x.Symbol == cmd[2]) != 1)
                    throw new Exception($"Cannot find {cmd[2]} in available instruments.{Environment.NewLine}{CommandSyntaxMessage}");
                instrument = SampleData.Instruments.Single(x => x.Exchange == account?.Exchange && x.Symbol == cmd[2]);

                if (!decimal.TryParse(cmd[3], out qty))
                    throw new Exception($"Cannot convert {cmd[3]} to quantity");

                if (!decimal.TryParse(cmd[4].Replace("@", ""), out price))
                    throw new Exception($"Cannot convert {cmd[4]} to price");
            }

            if (account == null || instrument == null || qty == 0 || price == 0)
                throw new Exception($"Incorrect syntax.{Environment.NewLine}{CommandSyntaxMessage}");

            string outputPrice = price == 999 ? "market" : price.ToString();
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
        public override string CommandSyntaxDetails => $"{"filter",15} :: Optional. [ open (default) | all | accountId | instrumentSymbol ] ";

        protected override Action<string[]> CommandAction => ListOrders;

        private void ListOrders(string[] cmd)
        {
            if (cmd.Length != 1)
                throw new Exception(CommandSyntaxMessage);

            _outputWindow.AppendText($"{Environment.NewLine}{"Order ID",-40}{"Inst",-8}{"QTY",-8}{"Price",-10}{"State",-10}{"Account",-30}");
            _outputWindow.AppendText($"--------------------------------------  ------  ------  --------  --------  ------------------------------");
            foreach (var item in SampleData.Orders)
            {
                _outputWindow.AppendText($"{item.ID,-40}{item.Instrument.Symbol,-8}{item.Qty,-8}{item.Price,-10}{item.State,-10}{item.Account.Name,-30}");
            }
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

            order.Single().State = OrderState.Canceled;
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
            _outputWindow.AppendText(_outputWindow.GetAliasesString());
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

            if (_outputWindow.RegisteredAliases.ContainsKey(alias))
                throw new Exception($"Alias {alias} already exists");

            if (Enum.TryParse(alias, out CommandName command))
                throw new Exception($"cannot use reserved word {alias} as an alias");

            _outputWindow.RegisteredAliases[alias] = val;
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

            if (!_outputWindow.RegisteredAliases.ContainsKey(alias))
                throw new Exception($"Alias {alias} does not exist exists");

            _outputWindow.RegisteredAliases.Remove(alias);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CLI_Sample
{
    public partial class swCLIWindow : UserControl
    {
        public swCLIWindow()
        {
            InitializeComponent();
            richTextBox1.Text = _commandPrompt;
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            txtAliases.Font = _unifiedSpaceFont;
            txtSelection.Font = _unifiedSpaceFont;
            UpdateTextBoxes();
        }

        private Font _unifiedSpaceFont = new Font(FontFamily.GenericSansSerif, 9);

        //private string _commandPrompt = "[:]";
        private Account _selectedAccount = null;
        private Instrument _selectedInstrument = null;
        private int _caretPositionOnCommandLineRelativeToLine = 0;
        private int _caretPositionOnCommandLineRelativeToWindow = 0;

        private int _previousCommandIndex = -1;
        private List<string> _previousCommands = new();

        private Dictionary<string, string> _aliases = new();
        private string _commandPromptHeader
        {
            get
            {
                if (_selectedInstrument != null && _selectedAccount != null)
                    return $"[:]{_selectedAccount.Exchange}: {_selectedAccount.Name} - {_selectedInstrument.Name}[:]";
                else if (_selectedAccount != null)
                    return $"[:]{_selectedAccount.Exchange}: {_selectedAccount.Name}[:]";
                else
                    return "";
            }
        }
        private string _commandPrompt
        {
            get
            {
                if (_selectedAccount != null)
                    return $">";
                else
                    return "[:]>";
            }
        }
        /*private string _commandPrompt
        {
            get
            {
                if (_selectedInstrument != null && _selectedAccount != null)
                    return $"[:]{_selectedAccount.Name} - {_selectedInstrument.Name}>";
                else if (_selectedAccount != null)
                    return $"[:]{_selectedAccount.Name}>";
                else
                    return "[:]>";
            }
        }*/

        private void richTextBox1_SelectionChanged(object sender, EventArgs e)
        {
            if (IsLastLine())
            {
                int currentLineIndex = richTextBox1.GetLineFromCharIndex(richTextBox1.SelectionStart);
                int lineStartIndex = richTextBox1.GetFirstCharIndexFromLine(currentLineIndex);
                if (richTextBox1.SelectionStart - lineStartIndex <= _commandPrompt.Length)
                {
                    int spaceToReduce = _commandPrompt.Length - (richTextBox1.SelectionStart - lineStartIndex);
                    richTextBox1.SelectionStart = lineStartIndex + _commandPrompt.Length;
                    if (richTextBox1.SelectionLength - spaceToReduce > 0)
                        richTextBox1.SelectionLength -= spaceToReduce;
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
                    ClearLastLine();
                    _previousCommandIndex--;
                    richTextBox1.AppendText(_previousCommands[_previousCommandIndex]);
                }
                e.Handled = true;
                return;
            }
            else if (e.KeyValue == 40) //down arrow
            {
                if (_previousCommandIndex != -1 && _previousCommandIndex < _previousCommands.Count - 1)
                {
                    ClearLastLine();
                    _previousCommandIndex++;
                    richTextBox1.AppendText(_previousCommands[_previousCommandIndex]);
                }
                e.Handled = true;
                return;
            }
            else if (e.KeyValue == 8) //backspace
            {
                if (!IsLastLine() || _caretPositionOnCommandLineRelativeToLine <= _commandPrompt.Length)
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

        private void RunCommand(string cmd)
        {
            bool addNewLineBeforePrompt = true;
            try
            {
                var text = cmd.Remove(0, _commandPrompt.Length);

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
                
                //string command = itemsInCmd[0].ToLowerInvariant();
                var cmdText = itemsInCmd[0].ToLowerInvariant();
                if (!Enum.TryParse<Commands>(cmdText, out Commands command))
                    throw new Exception($"No command {cmdText}. Type help for available commands");

                switch (command)
                {
                    case Commands.none:
                        break;
                    case Commands.account:
                        //addNewLineBeforePrompt = false;
                        SelectAccount(itemsInCmd);
                        break;
                    case Commands.accounts:
                        ListAccounts(itemsInCmd);
                        break;
                    case Commands.bal:
                        richTextBox1.AppendText($"{Environment.NewLine}Balances: One Million Dollars");
                        break;
                    case Commands.clear:
                        ClearOutput(itemsInCmd);
                        addNewLineBeforePrompt = false;
                        break;
                    case Commands.help:
                        PrintHelp();
                        break;
                    case Commands.instrument:
                        //addNewLineBeforePrompt = false;
                        SelectInstrument(itemsInCmd);
                        break;
                    case Commands.instruments:
                        ListInsruments(itemsInCmd);
                        break;
                    case Commands.buy:
                        Buy(itemsInCmd);
                        break;
                    case Commands.sell:
                        Sell(itemsInCmd);
                        break;
                    case Commands.orders:
                        ListOrders(itemsInCmd);
                        break;
                    case Commands.cancel:
                        CancelOrder(itemsInCmd);
                        break;
                    case Commands.aliases:
                        ListAliases(itemsInCmd);
                        break;
                    case Commands.alias:
                        CreateAlias(itemsInCmd);
                        break;
                    case Commands.unalias:
                        RemoveAlias(itemsInCmd);
                        break;
                    default:
                        throw new Exception($"No command {command}. Type help for available commands");
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendText($"Error: {ex.Message}", new Font(this.Font, FontStyle.Bold), Color.Red);
            }
            finally
            {
                if (addNewLineBeforePrompt)
                    richTextBox1.AppendText($"{Environment.NewLine}");
                AppendComandPromptHeader();
                richTextBox1.AppendText($"{_commandPrompt}");
                UpdateTextBoxes();
            }
        }

        private void AppendComandPromptHeader()
        {
            if (_selectedInstrument != null && _selectedAccount != null)
            {
                AppendText("[:]", Color.Green, false);
                AppendText(_selectedAccount.Exchange.ToString(), Color.Red, false);
                AppendText(_selectedAccount.Name.ToString(), Color.Purple, false);
                AppendText(_selectedInstrument.Name.ToString(), Color.Orange, false);
                AppendText("[:]", Color.Green);
            }
            else if (_selectedAccount != null)
            {
                AppendText("[:]", Color.Green, false);
                AppendText(_selectedAccount.Exchange.ToString(), Color.Red, false);
                AppendText(_selectedAccount.Name.ToString(), Color.Purple, false);
                AppendText("[:]", Color.Green);
            }
        }

        private string[] ApplyAliases(string[] itemsInCmd)
        {
            List<string> result = new();
            for (int i = 0; i < itemsInCmd.Length; i++)
            {
                if (_aliases.ContainsKey(itemsInCmd[i]))
                    result.AddRange(_aliases[itemsInCmd[i]].Split(" "));
                else
                    result.Add(itemsInCmd[i]);
            }
            return result.ToArray();
        }

        private void PrintHelp()
        {
            foreach(var command in Enum.GetNames(typeof(Commands)))
                AppendText($"\t{command}");
        }

        private void ClearOutput(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "clear", 0, "clear window");
            richTextBox1.ResetText();
        }

        private void SelectAccount(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "account", 1, "account selection");
            var accountsMatchingName = SampleData.Accounts.Where(x => x.Name == cmd[1]);
            if (accountsMatchingName.Count() == 0)
                throw new Exception($"No account with name {cmd[1]}");
            if (accountsMatchingName.Count() > 1)
                throw new Exception($"More than 1 account with name {cmd[1]}. Please rename accounts");

            _selectedAccount = accountsMatchingName.Single();
            _selectedInstrument = null;
            AppendText("");
        }

        private void SelectInstrument(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "instrument", 1, "instrument selection");
            if (_selectedAccount == null)
                throw new Exception("Cannot select instrument before selecting account. Use 'account' command");

            var matchingInstruments = SampleData.Instruments.Where(x => x.Exchange == _selectedAccount.Exchange && x.Symbol == cmd[1]);

            if (matchingInstruments.Count() == 0)
                throw new Exception($"No instrument with symbol {cmd[1]}");
            if (matchingInstruments.Count() > 1)
                throw new Exception($"More than 1 instrument with symbol {cmd[1]}. Please report this bug to the support team.");

            _selectedInstrument = matchingInstruments.Single();
            AppendText("");
        }

        private void ListAccounts(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "accounts", 0, "list accounts");
            foreach (var item in SampleData.Accounts)
            {
                AppendText(item.Name);
            }
            //AppendText(Environment.NewLine);
        }

        private void ListInsruments(string[] cmd)
        {
            if (_selectedAccount == null)
                throw new Exception("Cannot list instruments before selecting account. Use 'account' command");

            RunStandardCommandChecks(cmd, "instruments", 0, "list instruments");

            var font = new Font(FontFamily.GenericMonospace, richTextBox1.Font.Size);
            AppendText($"{Environment.NewLine}{"SYMBOL", -20}{"NAME",-25}", font);
            AppendText($"-----------------   -----------------------", font);
            foreach (var item in SampleData.Instruments.Where(x=>x.Exchange == _selectedAccount.Exchange))
            {
                AppendText($"{item.Symbol, -20}{item.Name,-25}", font);
            }
            AppendText(Environment.NewLine);
        }

        private void Buy(string[] cmd)
        {
            if (_selectedAccount == null)
                throw new Exception("Cannot buy before selecting an account. Use 'account' command");
            if (_selectedInstrument == null)
                throw new Exception("Cannot buy before selecting an instrument. Use 'instrument' command");

            if (cmd.Length == 2)
            {
                if (!decimal.TryParse(cmd[1], out decimal qty))
                    throw new Exception($"Cannot convert {cmd[1]} to quantity");

                AppendText($"buy {qty} {_selectedInstrument.Symbol} on account {_selectedAccount.Name} at market");
                SampleData.Orders.Add(new()
                {
                    Account = _selectedAccount,
                    Instrument = _selectedInstrument,
                    Qty = qty,
                    Price = 999,
                    Side = Side.Buy,
                    State = OrderState.Filled,
                });
            }

            if (cmd.Length == 3)
            {

                if (!decimal.TryParse(cmd[1], out decimal qty))
                    throw new Exception($"Cannot convert {cmd[1]} to quantity");

                if (!decimal.TryParse(cmd[2], out decimal price))
                    throw new Exception($"Cannot convert {cmd[2]} to quantity");

                AppendText($"place limit buy order at {price} for {qty} {_selectedInstrument.Symbol} on account {_selectedAccount.Name}");
                SampleData.Orders.Add(new()
                {
                    Account = _selectedAccount,
                    Instrument = _selectedInstrument,
                    Qty = qty,
                    Price = price,
                    Side = Side.Buy,
                    State = OrderState.Open,
                });
            }
        }

        private void Sell(string[] cmd)
        {
            if (_selectedAccount == null)
                throw new Exception("Cannot sell before selecting account. Use 'account' command");
            if (_selectedInstrument == null)
                throw new Exception("Cannot sell before selecting an instrument. Use 'instrument' command");

            if (cmd.Length == 2)
            {
                if (!decimal.TryParse(cmd[1], out decimal qty))
                    throw new Exception($"Cannot convert {cmd[1]} to quantity");

                AppendText($"sell {qty} {_selectedInstrument.Symbol} on account {_selectedAccount.Name} at market");
                SampleData.Orders.Add(new()
                {
                    Account = _selectedAccount,
                    Instrument = _selectedInstrument,
                    Qty = qty,
                    Price = 999,
                    Side = Side.Sell,
                    State = OrderState.Filled,
                });
            }

            if (cmd.Length == 3)
            {

                if (!decimal.TryParse(cmd[1], out decimal qty))
                    throw new Exception($"Cannot convert {cmd[1]} to quantity");

                if (!decimal.TryParse(cmd[2], out decimal price))
                    throw new Exception($"Cannot convert {cmd[2]} to quantity");

                AppendText($"place limit sell order at {price} for {qty} {_selectedInstrument.Symbol} on account {_selectedAccount.Name}");
                SampleData.Orders.Add(new()
                {
                    Account = _selectedAccount,
                    Instrument = _selectedInstrument,
                    Qty = qty,
                    Price = price,
                    Side = Side.Sell,
                    State = OrderState.Open,
                });
            }
        }

        private void ListOrders(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "orders", 0, "list orders");

            var font = new Font(FontFamily.GenericMonospace, richTextBox1.Font.Size);
            AppendText($"{Environment.NewLine}{"Order ID", -40}{"Inst",-8}{"QTY",-8}{"Price", -10}{"State", -10}{"Account", -30}", font);
            AppendText($"--------------------------------------  ------  ------  --------  --------  ------------------------------", font);
            foreach (var item in SampleData.Orders)
            {
                AppendText($"{item.ID,-40}{item.Instrument.Symbol,-8}{item.Qty,-8}{item.Price,-10}{item.State,-10}{item.Account.Name,-30}", font);
            }
        }

        private void CancelOrder(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "cancel", 1, "cancel order");

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

        private void ListAliases(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "aliases", 0, "list aliases");

            var font = new Font(FontFamily.GenericMonospace, richTextBox1.Font.Size);
            AppendText(GetAliasesString(), font);
        }

        private string GetAliasesString()
        {
            string aliases = $"{Environment.NewLine}{"Alias",-12}{"Replacement value",-30}";
            aliases += $"{Environment.NewLine}----------  ------------------------------";
            foreach (var item in _aliases)
            {
                aliases += $"{Environment.NewLine}{item.Key,-12}{item.Value,-30}";
            }
            return aliases;
        }

        private void CreateAlias(string[] cmd)
        {
            if (cmd[0].ToLower() != "alias")
                throw new Exception($"Not alias command");

            if (cmd.Length < 3)
                throw new Exception($"Too few parameters for alias command");

            string alias = cmd[1];

            if (alias.Length > 8)
                throw new Exception($"Alias cannot be more than 8 characters");

            string aliasValue = cmd[2];

            var x = cmd.Take(new Range(new Index(2), new Index(0, true)));

            var val = string.Join(" ", x);

            if (_aliases.ContainsKey(alias))
                throw new Exception($"Alias {alias} already exists");

            if (Enum.TryParse<Commands>(alias, out Commands command))
                throw new Exception($"cannot use reserved work {alias} as an alias");

            _aliases[alias] = val;
        }

        private void RemoveAlias(string[] cmd)
        {
            RunStandardCommandChecks(cmd, "unalias", 1, "remove alias");

            string alias = cmd[1];
            
            if (!_aliases.ContainsKey(alias))
                throw new Exception($"Alias {alias} does not exist exists");

            _aliases.Remove(alias);
        }

        private void RunStandardCommandChecks(string[] cmd, string command, int expectedParameterCount, string actionName)
        {
            if (cmd[0].ToLower() != command.ToLower())
                throw new Exception($"Not {actionName} command");
            if (cmd.Length < expectedParameterCount + 1) //add 1 for the command
                throw new Exception($"Too few parameters for {actionName} command");
            if (cmd.Length > expectedParameterCount + 1) //add 1 for the command
                throw new Exception($"Too many parameters for {actionName} command");
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

        private void AppendText(string text, bool autoReturn = true)
        {
            AppendText(text, null, null, autoReturn);
        }

        private void AppendText(string text, Color? textColor, bool autoReturn = true)
        {
            AppendText(text, null, textColor, autoReturn);
        }

        private void AppendText(string text, Font? font, bool autoReturn = true)
        {
            AppendText(text, font, null, autoReturn);
        }

        private void AppendText(string text, Font? font, Color? textColor, bool autoReturn = true)
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

        private void UpdateTextBoxes()
        {
            string txtSelectionText = "";
            txtSelectionText += $"{"Account:",-12}{_selectedAccount?.Name ?? "none",-30}";

            txtSelectionText += Environment.NewLine;

            txtSelectionText += $"{"Instrument:",-12}{_selectedInstrument?.Symbol ?? "none",-30}";
            txtSelection.Text = txtSelectionText;

            txtAliases.Text = GetAliasesString().Trim();
        }

        private void txt_SizeChanged(object sender, EventArgs e)
        {
            if (sender is Control ctrl)
            {
                toolTip1.SetToolTip(ctrl, $"Width:{ctrl.Width}{Environment.NewLine}Height:{ctrl.Height}");
            }
        }
    }

    public enum Commands
    {
        none,

        account,
        accounts,
        bal,
        clear,
        help,
        instrument,
        instruments,

        buy,
        sell,
        orders,
        cancel,

        aliases,
        alias,
        unalias
    }
}

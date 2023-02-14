namespace CLI_Sample
{
    partial class swCLIWindow
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.txtTextChanged = new System.Windows.Forms.TextBox();
            this.pnlDiagnostics = new System.Windows.Forms.Panel();
            this.splitter2 = new System.Windows.Forms.Splitter();
            this.txtAliases = new System.Windows.Forms.TextBox();
            this.txtSelection = new System.Windows.Forms.RichTextBox();
            this.txtKeyDown = new System.Windows.Forms.TextBox();
            this.splitter1 = new System.Windows.Forms.Splitter();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.pnlHelp = new System.Windows.Forms.Panel();
            this.txtHelp = new System.Windows.Forms.TextBox();
            this.pnlDiagnostics.SuspendLayout();
            this.pnlHelp.SuspendLayout();
            this.SuspendLayout();
            // 
            // richTextBox1
            // 
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.richTextBox1.Location = new System.Drawing.Point(0, 0);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(478, 363);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            this.richTextBox1.WordWrap = false;
            this.richTextBox1.SelectionChanged += new System.EventHandler(this.richTextBox1_SelectionChanged);
            this.richTextBox1.TextChanged += new System.EventHandler(this.richTextBox1_TextChanged);
            this.richTextBox1.KeyDown += new System.Windows.Forms.KeyEventHandler(this.richTextBox1_KeyDown);
            // 
            // txtTextChanged
            // 
            this.txtTextChanged.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtTextChanged.Location = new System.Drawing.Point(0, 274);
            this.txtTextChanged.Multiline = true;
            this.txtTextChanged.Name = "txtTextChanged";
            this.txtTextChanged.Size = new System.Drawing.Size(305, 89);
            this.txtTextChanged.TabIndex = 1;
            // 
            // pnlDiagnostics
            // 
            this.pnlDiagnostics.Controls.Add(this.splitter2);
            this.pnlDiagnostics.Controls.Add(this.txtAliases);
            this.pnlDiagnostics.Controls.Add(this.txtSelection);
            this.pnlDiagnostics.Controls.Add(this.txtKeyDown);
            this.pnlDiagnostics.Controls.Add(this.txtTextChanged);
            this.pnlDiagnostics.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlDiagnostics.Location = new System.Drawing.Point(591, 0);
            this.pnlDiagnostics.MinimumSize = new System.Drawing.Size(305, 0);
            this.pnlDiagnostics.Name = "pnlDiagnostics";
            this.pnlDiagnostics.Size = new System.Drawing.Size(305, 363);
            this.pnlDiagnostics.TabIndex = 2;
            // 
            // splitter2
            // 
            this.splitter2.Dock = System.Windows.Forms.DockStyle.Top;
            this.splitter2.Location = new System.Drawing.Point(0, 197);
            this.splitter2.Name = "splitter2";
            this.splitter2.Size = new System.Drawing.Size(305, 3);
            this.splitter2.TabIndex = 5;
            this.splitter2.TabStop = false;
            // 
            // txtAliases
            // 
            this.txtAliases.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtAliases.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtAliases.Location = new System.Drawing.Point(0, 50);
            this.txtAliases.Multiline = true;
            this.txtAliases.Name = "txtAliases";
            this.txtAliases.Size = new System.Drawing.Size(305, 147);
            this.txtAliases.TabIndex = 4;
            this.txtAliases.SizeChanged += new System.EventHandler(this.txt_SizeChanged);
            // 
            // txtSelection
            // 
            this.txtSelection.Dock = System.Windows.Forms.DockStyle.Top;
            this.txtSelection.Font = new System.Drawing.Font("Lucida Console", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtSelection.Location = new System.Drawing.Point(0, 0);
            this.txtSelection.Name = "txtSelection";
            this.txtSelection.Size = new System.Drawing.Size(305, 50);
            this.txtSelection.TabIndex = 3;
            this.txtSelection.Text = "No account selected\nNo Instrument selected";
            this.txtSelection.SizeChanged += new System.EventHandler(this.txt_SizeChanged);
            // 
            // txtKeyDown
            // 
            this.txtKeyDown.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtKeyDown.Location = new System.Drawing.Point(0, 193);
            this.txtKeyDown.Multiline = true;
            this.txtKeyDown.Name = "txtKeyDown";
            this.txtKeyDown.Size = new System.Drawing.Size(305, 81);
            this.txtKeyDown.TabIndex = 2;
            // 
            // splitter1
            // 
            this.splitter1.Dock = System.Windows.Forms.DockStyle.Right;
            this.splitter1.Location = new System.Drawing.Point(588, 0);
            this.splitter1.Name = "splitter1";
            this.splitter1.Size = new System.Drawing.Size(3, 363);
            this.splitter1.TabIndex = 5;
            this.splitter1.TabStop = false;
            // 
            // pnlHelp
            // 
            this.pnlHelp.Controls.Add(this.txtHelp);
            this.pnlHelp.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlHelp.Location = new System.Drawing.Point(478, 0);
            this.pnlHelp.MaximumSize = new System.Drawing.Size(110, 0);
            this.pnlHelp.MinimumSize = new System.Drawing.Size(110, 0);
            this.pnlHelp.Name = "pnlHelp";
            this.pnlHelp.Padding = new System.Windows.Forms.Padding(3);
            this.pnlHelp.Size = new System.Drawing.Size(110, 363);
            this.pnlHelp.TabIndex = 6;
            // 
            // txtHelp
            // 
            this.txtHelp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtHelp.Font = new System.Drawing.Font("Lucida Console", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtHelp.Location = new System.Drawing.Point(3, 3);
            this.txtHelp.Multiline = true;
            this.txtHelp.Name = "txtHelp";
            this.txtHelp.Size = new System.Drawing.Size(104, 357);
            this.txtHelp.TabIndex = 3;
            this.txtHelp.SizeChanged += new System.EventHandler(this.txt_SizeChanged);
            // 
            // swCLIWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.pnlHelp);
            this.Controls.Add(this.splitter1);
            this.Controls.Add(this.pnlDiagnostics);
            this.Name = "swCLIWindow";
            this.Size = new System.Drawing.Size(896, 363);
            this.pnlDiagnostics.ResumeLayout(false);
            this.pnlDiagnostics.PerformLayout();
            this.pnlHelp.ResumeLayout(false);
            this.pnlHelp.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private RichTextBox richTextBox1;
        private TextBox txtTextChanged;
        private Panel pnlDiagnostics;
        private TextBox txtKeyDown;
        private TextBox txtAliases;
        private RichTextBox txtSelection;
        private Splitter splitter1;
        private ToolTip toolTip1;
        private Splitter splitter2;
        private Panel pnlHelp;
        private TextBox txtHelp;
    }
}

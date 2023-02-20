namespace CLI_Sample
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.swcliWindow1 = new CLI_Sample.swCLIWindow();
            this.SuspendLayout();
            // 
            // swcliWindow1
            // 
            this.swcliWindow1.CancelTaskTriggered = false;
            this.swcliWindow1.CommandWaitingForUserInput = false;
            this.swcliWindow1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.swcliWindow1.Location = new System.Drawing.Point(0, 0);
            this.swcliWindow1.Name = "swcliWindow1";
            this.swcliWindow1.SelectedAccount = null;
            this.swcliWindow1.SelectedInstrument = null;
            this.swcliWindow1.Size = new System.Drawing.Size(1414, 715);
            this.swcliWindow1.TabIndex = 0;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1414, 715);
            this.Controls.Add(this.swcliWindow1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private swCLIWindow swcliWindow1;
    }
}
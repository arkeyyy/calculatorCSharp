namespace Calculator
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
            textBox1 = new TextBox();
            button1 = new Button();
            button2 = new Button();
            button3 = new Button();
            button4 = new Button();
            button5 = new Button();
            button6 = new Button();
            button7 = new Button();
            button8 = new Button();
            button9 = new Button();
            button0 = new Button();
            buttonDecimal = new Button();
            buttonEqual = new Button();
            buttonPlus = new Button();
            buttonMinus = new Button();
            buttonMultiply = new Button();
            buttonDivide = new Button();
            buttonBackspace = new Button();
            buttonClear = new Button();
            buttonDarkMode = new Button();
            textBoxHistory = new TextBox();
            SuspendLayout();
            // 
            // textBox1
            // 
            textBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBox1.Font = new Font("Segoe UI", 22.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            textBox1.Location = new Point(12, 52);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(486, 57);
            textBox1.TabIndex = 0;
            textBox1.TextAlign = HorizontalAlignment.Right;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Bottom;
            button1.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button1.Location = new Point(8, 190);
            button1.Name = "button1";
            button1.Size = new Size(118, 63);
            button1.TabIndex = 1;
            button1.Text = "1";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Anchor = AnchorStyles.Bottom;
            button2.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button2.Location = new Point(132, 190);
            button2.Name = "button2";
            button2.Size = new Size(118, 63);
            button2.TabIndex = 2;
            button2.Text = "2";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Anchor = AnchorStyles.Bottom;
            button3.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button3.Location = new Point(256, 190);
            button3.Name = "button3";
            button3.Size = new Size(118, 63);
            button3.TabIndex = 3;
            button3.Text = "3";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Anchor = AnchorStyles.Bottom;
            button4.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button4.Location = new Point(8, 259);
            button4.Name = "button4";
            button4.Size = new Size(118, 63);
            button4.TabIndex = 6;
            button4.Text = "4";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // button5
            // 
            button5.Anchor = AnchorStyles.Bottom;
            button5.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button5.Location = new Point(132, 259);
            button5.Name = "button5";
            button5.Size = new Size(118, 63);
            button5.TabIndex = 5;
            button5.Text = "5";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // button6
            // 
            button6.Anchor = AnchorStyles.Bottom;
            button6.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button6.Location = new Point(256, 259);
            button6.Name = "button6";
            button6.Size = new Size(118, 63);
            button6.TabIndex = 4;
            button6.Text = "6";
            button6.UseVisualStyleBackColor = true;
            button6.Click += button6_Click;
            // 
            // button7
            // 
            button7.Anchor = AnchorStyles.Bottom;
            button7.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button7.Location = new Point(8, 328);
            button7.Name = "button7";
            button7.Size = new Size(118, 63);
            button7.TabIndex = 9;
            button7.Text = "7";
            button7.UseVisualStyleBackColor = true;
            button7.Click += button7_Click;
            // 
            // button8
            // 
            button8.Anchor = AnchorStyles.Bottom;
            button8.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button8.Location = new Point(132, 328);
            button8.Name = "button8";
            button8.Size = new Size(118, 63);
            button8.TabIndex = 8;
            button8.Text = "8";
            button8.UseVisualStyleBackColor = true;
            button8.Click += button8_Click;
            // 
            // button9
            // 
            button9.Anchor = AnchorStyles.Bottom;
            button9.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button9.Location = new Point(256, 328);
            button9.Name = "button9";
            button9.Size = new Size(118, 63);
            button9.TabIndex = 7;
            button9.Text = "9";
            button9.UseVisualStyleBackColor = true;
            button9.Click += button9_Click;
            // 
            // button0
            // 
            button0.Anchor = AnchorStyles.Bottom;
            button0.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            button0.Location = new Point(132, 397);
            button0.Name = "button0";
            button0.Size = new Size(118, 63);
            button0.TabIndex = 10;
            button0.Text = "0";
            button0.UseVisualStyleBackColor = true;
            button0.Click += button0_Click;
            // 
            // buttonDecimal
            // 
            buttonDecimal.Anchor = AnchorStyles.Bottom;
            buttonDecimal.Font = new Font("Segoe UI", 19.8000011F, FontStyle.Bold);
            buttonDecimal.Location = new Point(8, 397);
            buttonDecimal.Name = "buttonDecimal";
            buttonDecimal.Size = new Size(118, 63);
            buttonDecimal.TabIndex = 11;
            buttonDecimal.Text = ".";
            buttonDecimal.UseVisualStyleBackColor = true;
            buttonDecimal.Click += buttonDecimal_Click;
            // 
            // buttonEqual
            // 
            buttonEqual.Anchor = AnchorStyles.Bottom;
            buttonEqual.BackColor = Color.FromArgb(255, 255, 128);
            buttonEqual.Font = new Font("Segoe UI", 19.8000011F, FontStyle.Bold);
            buttonEqual.Location = new Point(256, 397);
            buttonEqual.Name = "buttonEqual";
            buttonEqual.Size = new Size(242, 63);
            buttonEqual.TabIndex = 15;
            buttonEqual.Text = "=";
            buttonEqual.UseVisualStyleBackColor = false;
            buttonEqual.Click += buttonEqual_Click;
            // 
            // buttonPlus
            // 
            buttonPlus.Anchor = AnchorStyles.Bottom;
            buttonPlus.Font = new Font("Segoe UI", 19.8000011F, FontStyle.Bold);
            buttonPlus.Location = new Point(380, 328);
            buttonPlus.Name = "buttonPlus";
            buttonPlus.Size = new Size(118, 63);
            buttonPlus.TabIndex = 14;
            buttonPlus.Text = "+";
            buttonPlus.UseVisualStyleBackColor = true;
            buttonPlus.Click += buttonPlus_Click;
            // 
            // buttonMinus
            // 
            buttonMinus.Anchor = AnchorStyles.Bottom;
            buttonMinus.Font = new Font("Segoe UI", 19.8000011F, FontStyle.Bold);
            buttonMinus.Location = new Point(380, 259);
            buttonMinus.Name = "buttonMinus";
            buttonMinus.Size = new Size(118, 63);
            buttonMinus.TabIndex = 13;
            buttonMinus.Text = "-";
            buttonMinus.UseVisualStyleBackColor = true;
            buttonMinus.Click += buttonMinus_Click;
            // 
            // buttonMultiply
            // 
            buttonMultiply.Anchor = AnchorStyles.Bottom;
            buttonMultiply.Font = new Font("Segoe UI", 19.8000011F, FontStyle.Bold);
            buttonMultiply.Location = new Point(380, 190);
            buttonMultiply.Name = "buttonMultiply";
            buttonMultiply.Size = new Size(118, 63);
            buttonMultiply.TabIndex = 12;
            buttonMultiply.Text = "*";
            buttonMultiply.UseVisualStyleBackColor = true;
            buttonMultiply.Click += buttonMultiply_Click;
            // 
            // buttonDivide
            // 
            buttonDivide.Anchor = AnchorStyles.Bottom;
            buttonDivide.Font = new Font("Segoe UI", 16.2F, FontStyle.Bold, GraphicsUnit.Point, 0);
            buttonDivide.Location = new Point(380, 121);
            buttonDivide.Name = "buttonDivide";
            buttonDivide.Size = new Size(118, 63);
            buttonDivide.TabIndex = 19;
            buttonDivide.Text = "/";
            buttonDivide.UseVisualStyleBackColor = true;
            buttonDivide.Click += buttonDivide_Click;
            // 
            // buttonBackspace
            // 
            buttonBackspace.Anchor = AnchorStyles.Bottom;
            buttonBackspace.Font = new Font("Segoe UI", 19.8000011F, FontStyle.Bold, GraphicsUnit.Point, 0);
            buttonBackspace.Location = new Point(256, 121);
            buttonBackspace.Name = "buttonBackspace";
            buttonBackspace.Size = new Size(118, 63);
            buttonBackspace.TabIndex = 18;
            buttonBackspace.Text = "<";
            buttonBackspace.UseVisualStyleBackColor = true;
            buttonBackspace.Click += buttonBackspace_Click;
            // 
            // buttonClear
            // 
            buttonClear.Anchor = AnchorStyles.Bottom;
            buttonClear.BackColor = Color.FromArgb(255, 128, 128);
            buttonClear.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            buttonClear.ForeColor = Color.White;
            buttonClear.Location = new Point(132, 121);
            buttonClear.Name = "buttonClear";
            buttonClear.Size = new Size(118, 63);
            buttonClear.TabIndex = 17;
            buttonClear.Text = "AC";
            buttonClear.UseVisualStyleBackColor = false;
            buttonClear.Click += buttonClear_Click;
            // 
            // buttonDarkMode
            // 
            buttonDarkMode.Anchor = AnchorStyles.Bottom;
            buttonDarkMode.Font = new Font("Segoe UI", 10.2F, FontStyle.Bold);
            buttonDarkMode.Location = new Point(8, 121);
            buttonDarkMode.Name = "buttonDarkMode";
            buttonDarkMode.Size = new Size(118, 63);
            buttonDarkMode.TabIndex = 16;
            buttonDarkMode.Text = "DARKMODE";
            buttonDarkMode.UseVisualStyleBackColor = true;
            buttonDarkMode.Click += buttonDarkMode_Click;
            // 
            // textBoxHistory
            // 
            textBoxHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxHistory.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            textBoxHistory.Location = new Point(12, 12);
            textBoxHistory.Name = "textBoxHistory";
            textBoxHistory.Size = new Size(486, 34);
            textBoxHistory.TabIndex = 20;
            textBoxHistory.TextAlign = HorizontalAlignment.Right;
            textBoxHistory.TextChanged += textBoxHistory_TextChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(192, 192, 255);
            ClientSize = new Size(507, 467);
            Controls.Add(textBoxHistory);
            Controls.Add(buttonDivide);
            Controls.Add(buttonBackspace);
            Controls.Add(buttonClear);
            Controls.Add(buttonDarkMode);
            Controls.Add(buttonEqual);
            Controls.Add(buttonPlus);
            Controls.Add(buttonMinus);
            Controls.Add(buttonMultiply);
            Controls.Add(buttonDecimal);
            Controls.Add(button0);
            Controls.Add(button7);
            Controls.Add(button8);
            Controls.Add(button9);
            Controls.Add(button4);
            Controls.Add(button5);
            Controls.Add(button6);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(textBox1);
            Name = "Form1";
            Text = "Calculator";
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox textBox1;
        private Button button1;
        private Button button2;
        private Button button3;
        private Button button4;
        private Button button5;
        private Button button6;
        private Button button7;
        private Button button8;
        private Button button9;
        private Button button0;
        private Button buttonDecimal;
        private Button buttonEqual;
        private Button buttonPlus;
        private Button buttonMinus;
        private Button buttonMultiply;
        private Button buttonDivide;
        private Button buttonBackspace;
        private Button buttonClear;
        private Button buttonDarkMode;
        private TextBox textBoxHistory;
    }
}

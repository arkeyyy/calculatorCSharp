using System.Runtime.CompilerServices;

namespace Calculator
{
    public partial class Form1 : Form
    {
        string currentInput = "";
        double result = 0;
        string operation = "";
        bool operationPending = false;
        bool isDarkMode = false;
        bool isOpeartionLast = false;
        bool isEqualButtonClicked = false;
        bool anotherNumber = false;


        public Form1()
        {
            InitializeComponent();
        }


        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            
        }
        private void textBoxHistory_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Evaluate()
        {
            if (operationPending && !string.IsNullOrEmpty(operation))
            {
                double secondNumber = 0;

                if (!string.IsNullOrEmpty(currentInput))
                {
                    secondNumber = double.Parse(currentInput);
                }
                else
                {
                    // If no new input, just reuse last result
                    secondNumber = result;
                }

                switch (operation)
                {
                    case "+":
                        result += secondNumber;
                        break;
                    case "-":
                        result -= secondNumber;
                        break;
                    case "*":
                        result *= secondNumber;
                        break;
                    case "/":
                        if (secondNumber != 0)
                            result /= secondNumber;
                        else
                        {
                            textBox1.Text = "Cannot divide by zero";
                            currentInput = "";
                            operationPending = false;
                            return;
                        }
                        break;
                }

                textBox1.Text = result.ToString();
                currentInput = ""; // reset for next number
                operationPending = false;
                isOpeartionLast = false;
            }
        }

        private void numberButtonClicked(object sender, EventArgs e)
        {
            if (textBox1.Text == "Invalid Syntax" || textBox1.Text == "Cannot divide by zero")
            {
                isEqualButtonClicked = false;
                buttonClear_Click(sender, e);
            }
            Button button = (Button)sender;
            currentInput += button.Text;
            textBox1.Text = currentInput;
            textBoxHistory.Text += button.Text;
            isOpeartionLast = false;
            anotherNumber = false;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button3_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button4_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button5_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button6_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button7_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button8_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button9_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }
        private void button0_Click(object sender, EventArgs e)
        {
            numberButtonClicked(sender, e);
        }

        private void buttonDecimal_Click(object sender, EventArgs e)
        {
            if (!currentInput.Contains("."))
            {
                if (string.IsNullOrEmpty(currentInput))
                {
                    currentInput = "0.";
                    textBoxHistory.Text += "0.";
                }
                else
                {
                    currentInput += ".";
                    textBoxHistory.Text += ".";
                }

                textBox1.Text = currentInput;
                isOpeartionLast = false;
                anotherNumber = false;
            }
        }

        private void buttonPlus_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;

            if (isOpeartionLast)
            {
                textBoxHistory.Text = textBoxHistory.Text.Substring(0, textBoxHistory.Text.Length - 1);
            }
            else if (!string.IsNullOrEmpty(currentInput))
            {
                if (!operationPending)
                {
                    result = double.Parse(currentInput);
                }
                else
                {
                    Evaluate();
                }
            }

            operation = button.Text;
            textBoxHistory.Text += button.Text;
            currentInput = "";
            operationPending = true;
            isOpeartionLast = true;
            anotherNumber = true;
        }


        private void buttonMinus_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;

            if (isOpeartionLast)
            {
                textBoxHistory.Text = textBoxHistory.Text.Substring(0, textBoxHistory.Text.Length - 1);
            }
            else if (!string.IsNullOrEmpty(currentInput))
            {
                if (!operationPending)
                {
                    result = double.Parse(currentInput);
                }
                else
                {
                    Evaluate();
                }
            }

            operation = button.Text;
            textBoxHistory.Text += button.Text;
            currentInput = "";
            operationPending = true;
            isOpeartionLast = true;
            anotherNumber = true;
        }

        private void buttonMultiply_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;

            if (isOpeartionLast)
            {
                textBoxHistory.Text = textBoxHistory.Text.Substring(0, textBoxHistory.Text.Length - 1);
            }
            else if (!string.IsNullOrEmpty(currentInput))
            {
                if (!operationPending)
                {
                    result = double.Parse(currentInput);
                }
                else
                {
                    Evaluate();
                }
            }

            operation = button.Text;
            textBoxHistory.Text += button.Text;
            currentInput = "";
            operationPending = true;
            isOpeartionLast = true;
            anotherNumber = true;
        }

        private void buttonDivide_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;

            if (isOpeartionLast)
            {
                textBoxHistory.Text = textBoxHistory.Text.Substring(0, textBoxHistory.Text.Length - 1);
            }
            else if (!string.IsNullOrEmpty(currentInput))
            {
                if (!operationPending)
                {
                    result = double.Parse(currentInput);
                }
                else
                {
                    Evaluate();
                }
            }

            operation = button.Text;
            textBoxHistory.Text += button.Text;
            currentInput = "";
            operationPending = true;
            isOpeartionLast = true;
            anotherNumber = true;
        }

        private void buttonEqual_Click(object sender, EventArgs e)
        {
            buttonBackspace.BackColor = Color.FromArgb(255, 128, 128);
            buttonBackspace.ForeColor = Color.White;

            isEqualButtonClicked = true;
            if (textBoxHistory.Text[textBoxHistory.Text.Length - 1] == '+' ||
                textBoxHistory.Text[textBoxHistory.Text.Length - 1] == '-' ||
                textBoxHistory.Text[textBoxHistory.Text.Length - 1] == '*' ||
                textBoxHistory.Text[textBoxHistory.Text.Length - 1] == '/')
            {
                buttonClear_Click(sender, e);
                textBox1.Text = "Invalid Syntax";
                return;
            }
            Evaluate();
        }

        private void buttonBackspace_Click(object sender, EventArgs e)
        {
            if (isOpeartionLast)
            {
                isOpeartionLast = false;
                operationPending = false;
                operation = "";
            }

            if (isEqualButtonClicked)
            {
                buttonBackspace.BackColor = Color.White;
                buttonBackspace.ForeColor = Color.Black;
                buttonClear_Click(sender, e);
                isEqualButtonClicked = false;
                return;
            }

            if (!string.IsNullOrEmpty(textBoxHistory.Text))
            {
                char lastChar = textBoxHistory.Text[textBoxHistory.Text.Length - 1];

                if (char.IsDigit(lastChar) || lastChar == '.')
                {
                    textBoxHistory.Text = textBoxHistory.Text.Substring(0, textBoxHistory.Text.Length - 1);

                    if (!string.IsNullOrEmpty(currentInput))
                    {
                        currentInput = currentInput.Substring(0, currentInput.Length - 1);
                        textBox1.Text = currentInput;
                    }
                }
            }
        }


        private void buttonClear_Click(object sender, EventArgs e)
        {
            currentInput = "";
            result = 0;
            operation = "";
            operationPending = false;
            textBox1.Text = "";
            textBoxHistory.Text = "";
            anotherNumber = false;
        }

        private void buttonDarkMode_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Wa na implement ser kay murag problematic kaayo char gitapol na btaw ko next tym lng hehe", "Giduka nako ser", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

    }
}

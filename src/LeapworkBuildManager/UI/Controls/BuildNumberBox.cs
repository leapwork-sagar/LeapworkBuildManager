using System;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed class BuildNumberBox : DarkTextBox
    {
        bool formatting;
        static string Digits(string text)
        {
            var b = new System.Text.StringBuilder();
            foreach (char c in text)
                if (c >= '0' && c <= '9')
                    b.Append(c);
            return b.ToString();
        }

        public static string FormatDigits(string digits)
        {
            if (digits.Length < 4)
                return digits;
            if (digits.Length == 4)
                return digits + ".";
            if (digits.Length == 5)
                return digits.Substring(0, 4) + "." + digits.Substring(4) + ".";
            return digits.Substring(0, 4) + "." + digits.Substring(4, 1) + "." + digits.Substring(5);
        }

        int DigitPosition(int position)
        {
            return Digits(Text.Substring(0, Math.Min(position, Text.Length))).Length;
        }

        void Apply(string digits, int caret)
        {
            formatting = true;
            Text = FormatDigits(digits);
            int position = caret + (caret >= 4 ? 1 : 0) + (caret >= 5 ? 1 : 0);
            Select(Math.Min(position, Text.Length), 0);
            formatting = false;
            base.OnTextChanged(EventArgs.Empty);
        }

        public bool InsertNumericText(string text)
        {
            if (String.IsNullOrEmpty(text))
                return false;
            string value = text.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"\A(?:[0-9]+|[0-9]{4}\.[0-9]\.[0-9]+)\z"))
                return false;
            string inserted = Digits(value);
            string digits = Digits(Text);
            int start = DigitPosition(SelectionStart), length = DigitPosition(SelectionStart + SelectionLength) - start;
            Apply(digits.Remove(start, length).Insert(start, inserted), start + inserted.Length);
            return true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar >= '0' && e.KeyChar <= '9')
            {
                InsertNumericText(e.KeyChar.ToString());
                e.Handled = true;
            }
            else if (!Char.IsControl(e.KeyChar))
                e.Handled = true;
            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
            {
                string digits = Digits(Text);
                int start = DigitPosition(SelectionStart), length = DigitPosition(SelectionStart + SelectionLength) - start;
                if (length == 0)
                {
                    if (e.KeyCode == Keys.Back && start > 0)
                    {
                        start--;
                        length = 1;
                    }
                    else if (e.KeyCode == Keys.Delete && start < digits.Length)
                        length = 1;
                }

                if (length > 0)
                    Apply(digits.Remove(start, length), start);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x302)
            {
                try
                {
                    if (Clipboard.ContainsText())
                        InsertNumericText(Clipboard.GetText());
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                }

                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (formatting)
                return;
            string digits = Digits(Text);
            string normalized = FormatDigits(digits);
            if (Text != normalized)
            {
                int caret = DigitPosition(SelectionStart);
                Apply(digits, caret);
                return;
            }

            base.OnTextChanged(e);
        }
    }
}

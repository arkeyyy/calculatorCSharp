using System.Text.RegularExpressions;

namespace Calculator.Core;

/// <summary>Numeric text and editing selection survive focus moving to a keypad.</summary>
public sealed class NumericEntry
{
    public const int Limit = 128;
    private static readonly Regex EditableNumber = new(@"\A[+-]?(?:(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]*)(?:[eE][+-]?[0-9]*)?)?\z", RegexOptions.CultureInvariant);
    public string Text { get; private set; } = "";
    public int Anchor { get; private set; }
    public int Caret { get; private set; }
    public long Revision { get; private set; }

    public NumericEntry(string text = "0")
    {
        if (text.Length > Limit) throw new ArgumentException("Numeric entry is too long.");
        SetText(text);
        Select(0, text.Length);
    }

    public bool SetText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > Limit || value == Text || !EditableNumber.IsMatch(value)) return false;
        Text = value;
        Revision++;
        Select(value.Length, value.Length);
        return true;
    }

    public void Select(int anchor, int caret)
    {
        Anchor = Math.Clamp(anchor, 0, Text.Length);
        Caret = Math.Clamp(caret, 0, Text.Length);
    }

    public bool Press(string key)
    {
        int start = Math.Min(Anchor, Caret), end = Math.Max(Anchor, Caret);
        switch (key)
        {
            case "CE": case "C": SetText("0"); Select(1, 1); return true;
            case "⌫": case "Backspace":
                if (start != end || start > 0 || Text.Length == 0)
                {
                    if (start == end && start > 0) start--;
                    Replace(start, end, "");
                    if (Text.Length == 0) SetText("0");
                }
                return true;
            case "±": case "−":
                SetText(Text.StartsWith('-') ? Text[1..] : "-" + (Text.StartsWith('+') ? Text[1..] : Text));
                return true;
            case ".":
                string remaining = Text[..start] + Text[end..];
                if (!remaining.Contains('.') && !remaining.Contains('e', StringComparison.OrdinalIgnoreCase))
                {
                    if (start == 0 && end == 0 && HasLeadingSign) start = end = 1;
                    Replace(start, end, start == 0 || start == 1 && HasLeadingSign ? "0." : ".");
                }
                return true;
            default:
                if (key.Length != 1 || key[0] < '0' || key[0] > '9') return false;
                if (start == 0 && end == 0 && HasLeadingSign) start = end = 1;
                if ((Text == "0" || Text == "-0") && start == end && end == Text.Length) start = Text.Length - 1;
                Replace(start, end, key);
                return true;
        }
    }

    private bool HasLeadingSign => Text.StartsWith('-') || Text.StartsWith('+');
    private void Replace(int start, int end, string value)
    {
        string next = Text[..start] + value + Text[end..];
        if (next.Length > Limit) return;
        SetText(next);
        Select(start + value.Length, start + value.Length);
    }
}

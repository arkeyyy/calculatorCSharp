using System.Drawing.Drawing2D;

namespace Calculator.Controls;

public enum HeaderIcon { Menu, History }

public sealed class IconButton : StyledButton
{
    public HeaderIcon Icon { get; }

    public IconButton(HeaderIcon icon, string accessibleName, Action click, string name)
    {
        Icon = icon;
        Name = name;
        AccessibleName = accessibleName;
        AccessibleRole = AccessibleRole.PushButton;
        Text = "";
        FlatAppearance.BorderSize = 0;
        Click += (_, _) => click();
    }

    protected override void DrawContent(PaintEventArgs e, Rectangle bounds, Color foreground)
    {
        float scale = DeviceDpi / 96f;
        // One geometric center for every state, independent of font bearings or focus.
        float cx = ClientSize.Width / 2f, cy = ClientSize.Height / 2f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(foreground, Math.Max(1, 1.5f * scale)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (Icon == HeaderIcon.Menu)
        {
            foreach (int offset in new[] { -5, 0, 5 })
                e.Graphics.DrawLine(pen, cx - 8 * scale, cy + offset * scale, cx + 8 * scale, cy + offset * scale);
        }
        else
        {
            float radius = 9 * scale;
            e.Graphics.DrawEllipse(pen, cx - radius, cy - radius, 2 * radius, 2 * radius);
            e.Graphics.DrawLines(pen, [new PointF(cx, cy - 5 * scale), new PointF(cx, cy), new PointF(cx + 4 * scale, cy)]);
        }
    }
}

using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Calculator.Controls;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA2;

namespace Calculator.Ui.Tests;

[TestClass]
public sealed class AppearanceRenderingTests
{
    [DataTestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void HeaderIconAndHighlightStayCenteredThroughNativeMouseStates(string theme)
    {
        var palette = ThemePalette.Create(theme, "BLUE");
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form { ClientSize = new Size(360, 180) };
            var icon = new IconButton(HeaderIcon.Menu, "Open navigation", () => { }, "icon");
            int size = (int)Math.Round(36 * host.DeviceDpi / 96d);
            icon.SetBounds(40, 30, size, size);
            var other = Calculator.Controls.Ui.Button("Other", () => { }, "other");
            other.SetBounds(150, 30, 100, size);
            host.Controls.AddRange([icon, other]);
            palette.Apply(host);
            icon.BackColor = palette.Background;
            return host;
        });
        MoveMouse(ui.Read(host => host.PointToScreen(new Point(300, 120))));
        ui.Do(host => UiThreadHarness.Find<Button>(host, "other").Focus());
        using var normal = Capture(ui);
        Point center = ui.Read(host =>
        {
            var icon = UiThreadHarness.Find<IconButton>(host, "icon");
            return icon.PointToScreen(new Point(icon.Width / 2, icon.Height / 2));
        });
        MoveMouse(center);
        Wait.UntilInputIsProcessed();
        using var hover = Capture(ui);
        Mouse.Down(MouseButton.Left);
        try
        {
            Wait.UntilInputIsProcessed();
            using var pressed = Capture(ui);
            ui.Do(host => Assert.IsTrue(UiThreadHarness.Find<IconButton>(host, "icon").AccessibilityObject.State.HasFlag(AccessibleStates.Pressed),
                "Native accessibility must report the mouse-pressed state."));
            foreach (Bitmap bitmap in new[] { normal, hover, pressed })
                AssertCenteredInk(bitmap, palette.Text, ui.Dpi);
            int sample = (int)Math.Round(6 * ui.Dpi / 96d);
            Assert.AreNotEqual(normal.GetPixel(sample, sample), hover.GetPixel(sample, sample), "Actual pointer entry must show a hover background.");
            Assert.AreNotEqual(hover.GetPixel(sample, sample), pressed.GetPixel(sample, sample), "Actual mouse press must have a separate state.");
            AssertCenteredColor(hover, hover.GetPixel(sample, sample));
            AssertCenteredColor(pressed, pressed.GetPixel(sample, sample));
            Save(hover, $"hamburger-{theme}-hover-{ui.Dpi}dpi");
            Save(pressed, $"hamburger-{theme}-pressed-{ui.Dpi}dpi");
        }
        finally { Mouse.Up(MouseButton.Left); }
        ui.Do(host => UiThreadHarness.Find<IconButton>(host, "icon").Enabled = false);
        using var disabled = Capture(ui);
        AssertCenteredInk(disabled, palette.Muted, ui.Dpi);
    }

    [TestMethod]
    public void StyledButtonsKeepKeyboardAndAccessibleActivation()
    {
        int presses = 0;
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form();
            var other = Calculator.Controls.Ui.Button("Other", () => { }, "other");
            other.SetBounds(10, 10, 100, 40);
            other.TabIndex = 0;
            var icon = new IconButton(HeaderIcon.Menu, "Open navigation", () => presses++, "icon") { TabIndex = 1 };
            icon.SetBounds(120, 10, 45, 45);
            host.Controls.AddRange([other, icon]);
            ThemePalette.Create("dark", "YELLOW").Apply(host);
            return host;
        });
        ui.Do(host => UiThreadHarness.Find<Button>(host, "other").Focus());
        ui.Key(VirtualKeyShort.TAB);
        Assert.AreEqual("icon", ui.FocusedName);
        ui.Do(host => SendMessage(host.Handle, 0x0127, new IntPtr(0x10002), IntPtr.Zero));
        using var focused = Capture(ui);
        Save(focused, $"hamburger-keyboard-focus-{ui.Dpi}dpi");
        ui.Key(VirtualKeyShort.SPACE);
        ui.Key(VirtualKeyShort.RETURN);
        Assert.AreEqual(2, ui.Read(_ => presses));
        ui.Do(host =>
        {
            var button = UiThreadHarness.Find<IconButton>(host, "icon");
            Assert.AreEqual(AccessibleRole.PushButton, button.AccessibilityObject.Role);
            Assert.AreEqual("Open navigation", button.AccessibilityObject.Name);
            button.AccessibilityObject.DoDefaultAction();
            button.Selected = true;
            Assert.IsTrue(button.AccessibilityObject.State.HasFlag(AccessibleStates.Pressed));
        });
        Assert.AreEqual(3, ui.Read(_ => presses));
        using var automation = new UIA2Automation();
        var iconElement = automation.FromHandle(ui.Handle).FindFirstDescendant(c => c.ByAutomationId("icon"));
        Assert.IsNotNull(iconElement, "The styled icon must remain discoverable by native automation.");
        Assert.AreEqual("Open navigation", iconElement.Name);
        Assert.IsTrue(iconElement.Patterns.Invoke.IsSupported, "Native button Invoke support must survive custom painting.");
        iconElement.Patterns.Invoke.Pattern.Invoke();
        ui.WaitUntil(_ => presses == 4, "Accessibility Invoke did not activate the styled button.");
    }

    [DataTestMethod]
    [DataRow("light")]
    [DataRow("dark")]
    public void PrimaryButtonTextKeepsContrastForEveryAccentAndPointerState(string theme)
    {
        using var ui = new UiThreadHarness(() =>
        {
            var host = new Form { ClientSize = new Size(320, 180) };
            var button = Calculator.Controls.Ui.Button("Refresh", () => { }, "primary");
            button.Tag = "primary";
            button.SetBounds(20, 20, 160, 50);
            host.Controls.Add(button);
            return host;
        });
        foreach (string accent in ThemePalette.AccentNames)
        {
            ui.Do(host => ThemePalette.Create(theme, accent).Apply(host));
            MoveMouse(ui.Read(host => host.PointToScreen(new Point(280, 140))));
            Wait.UntilInputIsProcessed();
            AssertRenderedContrast(ui, theme + "/" + accent + "/normal");
            MoveMouse(ui.Read(host => UiThreadHarness.Find<Button>(host, "primary").PointToScreen(new Point(80, 25))));
            Wait.UntilInputIsProcessed();
            AssertRenderedContrast(ui, theme + "/" + accent + "/hover");
            Mouse.Down(MouseButton.Left);
            try
            {
                Wait.UntilInputIsProcessed();
                AssertRenderedContrast(ui, theme + "/" + accent + "/pressed");
            }
            finally { Mouse.Up(MouseButton.Left); }
        }
    }

    private static void AssertRenderedContrast(UiThreadHarness ui, string state) => ui.Do(host =>
    {
        var button = UiThreadHarness.Find<Button>(host, "primary");
        using var image = new Bitmap(button.Width, button.Height);
        button.DrawToBitmap(image, button.ClientRectangle);
        Color background = image.GetPixel(10, 10);
        int black = 0, white = 0;
        // Sample the text region, excluding the border and keyboard focus outline.
        for (int y = 10; y < image.Height - 10; y++)
            for (int x = 20; x < image.Width - 20; x++)
            {
                int pixel = image.GetPixel(x, y).ToArgb();
                if (pixel == Color.Black.ToArgb()) black++;
                else if (pixel == Color.White.ToArgb()) white++;
            }
        Assert.IsTrue(black + white > 0, "No primary text rendered in " + state);
        Color foreground = black > white ? Color.Black : Color.White;
        static double Luminance(Color color)
        {
            static double Linear(byte component) { double value = component / 255d; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        double a = Luminance(foreground), b = Luminance(background);
        double contrast = (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        Assert.IsTrue(contrast >= 4.5, $"{state} has only {contrast:F2}:1 rendered text contrast.");
    });

    private static Bitmap Capture(UiThreadHarness ui) => ui.Read(host =>
    {
        var icon = UiThreadHarness.Find<IconButton>(host, "icon");
        var bitmap = new Bitmap(icon.Width, icon.Height);
        icon.DrawToBitmap(bitmap, icon.ClientRectangle);
        return bitmap;
    });

    private static void AssertCenteredInk(Bitmap image, Color ink, int dpi)
    {
        int inset = (int)Math.Round(6 * dpi / 96d);
        Rectangle bounds = FindColor(image, ink, Rectangle.Inflate(new Rectangle(Point.Empty, image.Size), -inset, -inset));
        Assert.IsFalse(bounds.IsEmpty, "The icon must render with its themed foreground color.");
        AssertCenter(image, bounds);
    }

    private static void AssertCenteredColor(Bitmap image, Color color) =>
        AssertCenter(image, FindColor(image, color, new Rectangle(Point.Empty, image.Size)));

    private static void AssertCenter(Bitmap image, Rectangle bounds)
    {
        Assert.IsTrue(Math.Abs((bounds.Left + bounds.Right) / 2d - image.Width / 2d) <= 1, $"Horizontal center shifted: {bounds}, bitmap {image.Size}.");
        Assert.IsTrue(Math.Abs((bounds.Top + bounds.Bottom) / 2d - image.Height / 2d) <= 1, $"Vertical center shifted: {bounds}, bitmap {image.Size}.");
    }

    private static Rectangle FindColor(Bitmap image, Color color, Rectangle search)
    {
        int left = image.Width, top = image.Height, right = -1, bottom = -1;
        for (int y = search.Top; y < search.Bottom; y++)
            for (int x = search.Left; x < search.Right; x++)
                if (image.GetPixel(x, y).ToArgb() == color.ToArgb())
                { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static void MoveMouse(Point point)
    {
        IntPtr old = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try { Mouse.MoveTo(point); }
        finally { SetThreadDpiAwarenessContext(old); }
    }

    private static void Save(Bitmap image, string name)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "screenshots"));
        Directory.CreateDirectory(path);
        image.Save(Path.Combine(path, name + ".png"), ImageFormat.Png);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
}

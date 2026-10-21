using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Calculator.Controls;
using Calculator.Views;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

[assembly: DoNotParallelize]

namespace Calculator.Ui.Tests;

/// <summary>Runs each fixture on its own native STA message pump.</summary>
internal sealed class UiThreadHarness : IDisposable
{
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new();
    private Form? form;
    private Exception? failure;

    public UiThreadHarness(Func<Form> create, bool acquireForeground = true)
    {
        thread = new Thread(() =>
        {
            try
            {
                SetThreadDpiAwarenessContext(new IntPtr(-4));
                Application.EnableVisualStyles();
                form = create();
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(40, 40);
                form.Shown += (_, _) => ready.Set();
                Application.Run(form);
            }
            catch (Exception exception) { failure = exception; ready.Set(); }
        }) { IsBackground = true, Name = "Calculator UI verification" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("The test form did not open.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        if (!acquireForeground) return;
        // A native title-bar click gives SendInput a real foreground target. Merely calling
        // Activate can be refused by Windows when the test runner starts in the background.
        Point titleBar = Read(host =>
        {
            ShowWindow(host.Handle, 9);
            host.TopMost = true;
            host.Activate();
            SetForegroundWindow(host.Handle);
            var point=new Point(host.Left + 64, host.Top + Math.Max(8, SystemInformation.CaptionHeight / 2));
            return point;
        });
        // Screen coordinates came from the DPI-aware UI thread. Use the same context
        // for native mouse input so scaling cannot redirect a click to a caption button.
        if(Read(host=>GetForegroundWindow()!=host.Handle))
        {
            IntPtr inputDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
            try { Mouse.Click(titleBar); }
            finally { SetThreadDpiAwarenessContext(inputDpi); }
        }
        Wait.UntilInputIsProcessed();
        Do(host => { SetForegroundWindow(host.Handle); host.TopMost = false; });
        try { WaitUntil(host => GetForegroundWindow() == host.Handle, "The test window could not acquire the foreground for native input."); }
        catch { Dispose(); throw; }
        Wait.UntilInputIsProcessed();
    }

    public T Read<T>(Func<Form, T> action)
    {
        if (form is null) throw new InvalidOperationException("The UI form is unavailable.");
        try { return (T)form.Invoke(() => action(form)); }
        catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }

    public void Do(Action<Form> action) => Read(host => { action(host); return 0; });
    public IntPtr Handle => Read(host => host.Handle);
    public int Dpi => Read(host => host.DeviceDpi);
    public string FocusedName => Read(_ => Control.FromHandle(GetFocus())?.Name ?? "native popup");
    public string Text(string name) => Read(host => Find<Control>(host, name).Text);

    public void Click(string name) => Do(host =>
    {
        ButtonBase button = Find<ButtonBase>(host, name);
        button.Focus();
        switch (button)
        {
            case Button command: command.PerformClick(); break;
            case RadioButton choice: choice.PerformClick(); break;
            default: throw new AssertFailedException($"Unsupported button type: {button.GetType().Name}");
        }
    });

    public void Key(VirtualKeyShort key)
    {
        Keyboard.Type(key);
        Wait.UntilInputIsProcessed();
        Do(_ => { });
    }

    public void Chord(params VirtualKeyShort[] keys)
    {
        Keyboard.TypeSimultaneously(keys);
        Wait.UntilInputIsProcessed();
        Do(_ => { });
    }

    public void WaitUntil(Func<Form, bool> predicate, string message, int milliseconds = 5000)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (Read(predicate)) return;
            Thread.Sleep(20);
        }
        Assert.Fail(message);
    }

    public void WaitForNavigation() => WaitUntil(host => !((MainForm)host).NavigationTransition.IsRunning,
        "Navigation did not finish its transition.");

    public static T Find<T>(Control root, string name) where T : Control =>
        ModeView.Descendants(root).OfType<T>().FirstOrDefault(control => control.Name == name)
        ?? throw new AssertFailedException($"Control '{name}' ({typeof(T).Name}) was not found.");

    public static void AssertKeypadBounds(Control root)
    {
        foreach (KeypadControl keypad in ModeView.Descendants(root).OfType<KeypadControl>().Where(keypad => keypad.Visible))
        {
            int minimum = (int)Math.Round(34 * keypad.DeviceDpi / 96d);
            Button[] buttons = keypad.Buttons.Where(button => button.Visible).ToArray();
            Assert.IsTrue(buttons.Length > 0, "A visible keypad has no visible keys.");
            foreach (Button button in buttons)
            {
                Assert.IsTrue(button.Left >= 0 && button.Top >= 0 && button.Right <= keypad.ClientSize.Width && button.Bottom <= keypad.ClientSize.Height,
                    $"{button.Name} at {button.Bounds} escapes keypad {keypad.ClientSize}, DPI={keypad.DeviceDpi}.");
                Assert.IsTrue(button.Height >= minimum,
                    $"{button.Name} height {button.Height} is below 34 logical pixels at DPI={keypad.DeviceDpi}.");
            }
            for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                    Assert.IsFalse(buttons[i].Bounds.IntersectsWith(buttons[j].Bounds), $"{buttons[i].Name} overlaps {buttons[j].Name}.");
        }
    }

    public void Dispose()
    {
        if (form is not null && !form.IsDisposed)
        {
            try { form.Invoke(() => { form.Close(); form.Dispose(); }); }
            catch (InvalidOperationException) { }
        }
        if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("The test UI did not close.");
        ready.Dispose();
        form = null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window,int command);
}

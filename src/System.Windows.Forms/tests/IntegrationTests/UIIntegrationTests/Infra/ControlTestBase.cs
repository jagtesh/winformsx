// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms.UITests.Input;
using Microsoft.VisualStudio.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

[UseDefaultXunitCulture]
[UISettings(MaxAttempts = 3)] // Try up to 3 times before failing.
public abstract class ControlTestBase : IAsyncLifetime, IDisposable
{
    private const int SPIF_SENDCHANGE = 0x0002;
    private static readonly TimeSpan s_winFormsXTestTimeout = TimeSpan.FromSeconds(30);

    private bool _clientAreaAnimation;
    private JoinableTaskCollection _joinableTaskCollection = null!;
    private static string s_previousRunTestName = "This is the first test to run.";
    private readonly string? _previousSuppressHiddenBackend;

    private Point? _mousePosition;

    static ControlTestBase()
    {
        DataCollectionService.InstallFirstChanceExceptionHandler();
    }

    protected ControlTestBase(ITestOutputHelper testOutputHelper)
    {
        TestOutputHelper = testOutputHelper;
        DataCollectionService.CurrentTest = GetTest();
        testOutputHelper.WriteLine($" Previous run test: {s_previousRunTestName}");
        s_previousRunTestName = DataCollectionService.CurrentTest.DisplayName;

        Application.EnableVisualStyles();
        _previousSuppressHiddenBackend = Environment.GetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND");
        Environment.SetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1");

        // Disable animations for maximum test performance
        bool disabled = false;
        Assert.True(PInvokeCore.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, ref _clientAreaAnimation));
        Assert.True(PInvokeCore.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, ref disabled, SPIF_SENDCHANGE));

        ITest GetTest()
        {
            var type = testOutputHelper.GetType();
            var testMember = type.GetField("test", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (ITest)testMember.GetValue(testOutputHelper)!;
        }
    }

    protected ITestOutputHelper TestOutputHelper { get; }

    protected JoinableTaskContext JoinableTaskContext { get; private set; } = null!;

    protected JoinableTaskFactory JoinableTaskFactory { get; private set; } = null!;

    protected SendInput InputSimulator => new(WaitForIdleAsync);

    public virtual Task InitializeAsync()
    {
        CloseOpenForms();

        // Verify keyboard and mouse state at the start of the test
        VerifyKeyStates(isStartOfTest: true, TestOutputHelper);

        // Record the mouse position so it can be restored at the end of the test
        _mousePosition = Cursor.Position;

        JoinableTaskContext = new JoinableTaskContext();

        _joinableTaskCollection = JoinableTaskContext.CreateCollection();
        JoinableTaskFactory = JoinableTaskContext.CreateFactory(_joinableTaskCollection);
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        await _joinableTaskCollection.JoinTillEmptyAsync();

        // Verify keyboard and mouse state at the end of the test
        VerifyKeyStates(isStartOfTest: false, TestOutputHelper);

        // Restore the mouse position
        if (_mousePosition is { } mousePosition)
        {
            Cursor.Position = mousePosition;
        }

        JoinableTaskContext = null!;
        JoinableTaskFactory = null!;
        CloseOpenForms();
    }

    public virtual void Dispose()
    {
        Assert.True(PInvokeCore.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, ref _clientAreaAnimation));
        Environment.SetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", _previousSuppressHiddenBackend);
        DataCollectionService.CurrentTest = null;
    }

    private void VerifyKeyStates(bool isStartOfTest, ITestOutputHelper testOutputHelper)
    {
        // Verify that no window has currently captured the cursor
        Assert.Equal(HWND.Null, PInvoke.GetCapture());

        // Verify that no keyboard or mouse keys are in the pressed state at the beginning of the test, since
        // this could interfere with test behavior. This code uses GetAsyncKeyState since GetKeyboardState was
        // not working reliably in local testing.
        foreach (var code in Enum.GetValues<VIRTUAL_KEY>())
        {
            if (code is VIRTUAL_KEY.VK_SCROLL or VIRTUAL_KEY.VK_NUMLOCK)
                continue;

            if (PInvoke.GetAsyncKeyState((int)code) < 0)
            {
                // 😕 VK_LEFT and VK_RIGHT was observed to be pressed at the start of a test even though no test
                // ran before it
                if (isStartOfTest && code is VIRTUAL_KEY.VK_LEFT or VIRTUAL_KEY.VK_RIGHT)
                {
                    testOutputHelper.WriteLine($"Sending WM_KEYUP for '{code}' at the start of the test");
                    new InputSimulator().Keyboard.KeyUp(code);
                }
                else
                {
                    Assert.Fail($"The key with virtual key code '{code}' was unexpectedly pressed at the {(isStartOfTest ? "start" : "end")} of the test.");
                }
            }
        }
    }

    private static void CloseOpenForms()
    {
        Form[] openForms = Application.OpenForms.Cast<Form>().ToArray();
        foreach (Form form in openForms)
        {
            if (!form.IsDisposed)
            {
                form.Close();
                form.Dispose();
            }
        }
    }

    protected async Task WaitForIdleAsync()
    {
        await Task.Yield();
        Application.DoEvents();
    }

    protected async Task MoveMouseToControlAsync(Control control)
    {
        var rect = control.DisplayRectangle;
        var centerOfRect = GetCenter(rect);
        var centerOnScreen = control.PointToScreen(centerOfRect);
        await MoveMouseAsync(control.FindForm()!, centerOnScreen);
    }

    protected internal static Point ToVirtualPoint(Point point)
    {
        Size primaryMonitor = SystemInformation.PrimaryMonitorSize;
        return new Point(
            (int)Math.Ceiling((65535.0 / (primaryMonitor.Width - 1)) * point.X),
            (int)Math.Ceiling((65535.0 / (primaryMonitor.Height - 1)) * point.Y));
    }

    protected async Task MoveMouseAsync(Form window, Point point, bool assertCorrectLocation = true)
    {
        TestOutputHelper.WriteLine($"Moving mouse to ({point.X}, {point.Y}).");
        Size primaryMonitor = SystemInformation.PrimaryMonitorSize;
        var virtualPoint = ToVirtualPoint(point);
        TestOutputHelper.WriteLine($"Screen resolution of ({primaryMonitor.Width}, {primaryMonitor.Height}) translates mouse to ({virtualPoint.X}, {virtualPoint.Y}).");

        await InputSimulator.SendAsync(window, inputSimulator => inputSimulator.Mouse.MoveMouseTo(virtualPoint.X, virtualPoint.Y));

        // ⚠ The call to GetCursorPos is required for correct behavior.
        if (!PInvoke.GetCursorPos(out Point actualPoint))
        {
#pragma warning disable CS8597 // Thrown value may be null.
            throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
#pragma warning restore CS8597 // Thrown value may be null.
        }

        if (actualPoint.X != point.X || actualPoint.Y != point.Y)
        {
            // Wait and try again
            await Task.Delay(15);
            if (!PInvoke.GetCursorPos(out Point _))
            {
#pragma warning disable CS8597 // Thrown value may be null.
                throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
#pragma warning restore CS8597 // Thrown value may be null.
            }
        }

        if (assertCorrectLocation)
        {
            Assert.Equal(point, actualPoint);
        }
    }

    protected async Task RunSingleControlTestAsync<T>(Func<Form, T, Task> testDriverAsync)
        where T : Control, new()
    {
        await RunFormAsync(
            () =>
            {
                Form form = new()
                {
                    TopMost = true
                };

                T control = new();
                form.Controls.Add(control);

                return (form, control);
            },
            testDriverAsync);
    }

    protected async Task RunSingleControlTestAsync<T>(Func<Form, T, Task> testDriverAsync, Func<T> createControl, Func<Form>? createForm = null)
        where T : Control, new()
    {
        await RunFormAsync(
            () =>
            {
                Form form;
                if (createForm is null)
                {
                    form = new();
                }
                else
                {
                    form = createForm();
                }

                form.TopMost = true;

                T control = createControl();
                Assert.NotNull(control);

                form.Controls.Add(control);

                return (form, control);
            },
            testDriverAsync);
    }

    protected async Task RunControlPairTestAsync<T1, T2>(Func<Form, (T1 control1, T2 control2), Task> testDriverAsync)
        where T1 : Control, new()
        where T2 : Control, new()
    {
        await RunFormAsync(
            () =>
            {
                Form form = new()
                {
                    TopMost = true
                };

                var control1 = new T1();
                var control2 = new T2();

                TableLayoutPanel tableLayout = new()
                {
                    ColumnCount = 2,
                    RowCount = 1
                };
                tableLayout.Controls.Add(control1, 0, 0);
                tableLayout.Controls.Add(control2, 1, 0);
                form.Controls.Add(tableLayout);

                return (form, (control1, control2));
            },
            testDriverAsync);
    }

    protected async Task RunFormAsync<T>(Func<(Form dialog, T control)> createDialog, Func<Form, T, Task> testDriverAsync)
    {
        using var screenRecordService = new ScreenRecordService();
        var (dialog, control) = createDialog();
        screenRecordService.RegisterEvents(dialog);

        Assert.NotNull(dialog);
        Assert.NotNull(control);

        CreateControlWithoutHiddenBackend(dialog);
        dialog.Show();
        ActivateWinFormsXDialog(dialog);
        await WaitForIdleAsync();
        try
        {
            await RunWithWinFormsXTimeoutAsync(
                () => testDriverAsync(dialog, control),
                dialog);
        }
        finally
        {
            dialog.Close();
            dialog.Dispose();
        }
    }

    protected async Task RunFormWithoutControlAsync<TForm>(Func<TForm> createForm, Func<TForm, Task> testDriverAsync)
        where TForm : Form
    {
        using var screenRecordService = new ScreenRecordService();
        TForm dialog = createForm();
        screenRecordService.RegisterEvents(dialog);

        Assert.NotNull(dialog);

        CreateControlWithoutHiddenBackend(dialog);
        dialog.Show();
        ActivateWinFormsXDialog(dialog);
        await WaitForIdleAsync();
        try
        {
            await RunWithWinFormsXTimeoutAsync(
                () => testDriverAsync(dialog),
                dialog);
        }
        finally
        {
            dialog.Close();
            dialog.Dispose();
        }
    }

    internal struct VoidResult
    {
    }

    private static void CreateControlWithoutHiddenBackend(Control control)
    {
        string? previous = Environment.GetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND");
        Environment.SetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1");
        try
        {
            _ = control.Handle;
            control.CreateControl();
            foreach (Control child in control.Controls)
            {
                CreateControlWithoutHiddenBackend(child);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", previous);
        }
    }

    private static void ActivateWinFormsXDialog(Form dialog)
    {
        PInvoke.SetActiveWindow(dialog);
        PInvoke.SetForegroundWindow(dialog);
        PInvoke.SetFocus(GetInitialFocusTarget(dialog));
    }

    private static Control GetInitialFocusTarget(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.TabStop && child.Enabled)
            {
                return child;
            }

            Control? nested = GetInitialFocusTargetOrNull(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return parent;
    }

    private static Control? GetInitialFocusTargetOrNull(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.TabStop && child.Enabled)
            {
                return child;
            }

            Control? nested = GetInitialFocusTargetOrNull(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    internal static Point GetCenter(Rectangle cell)
    {
        return new Point(GetMiddle(cell.Right, cell.Left), GetMiddle(cell.Top, cell.Bottom));

        static int GetMiddle(int a, int b) => a + ((b - a) / 2);
    }

    private async Task RunWithWinFormsXTimeoutAsync(Func<Task> action, Form dialog)
    {
        Task timeoutTask = Task.Delay(s_winFormsXTestTimeout);
        Task actionTask = action();
        Task completed = await Task.WhenAny(actionTask, timeoutTask);
        if (ReferenceEquals(completed, actionTask))
        {
            await actionTask;
            return;
        }

        TestOutputHelper.WriteLine($"Timed out after {s_winFormsXTestTimeout.TotalSeconds:F0}s in '{DataCollectionService.CurrentTest?.DisplayName}'.");
        TestOutputHelper.WriteLine($"Dialog.Visible={dialog.Visible} Dialog.IsDisposed={dialog.IsDisposed} Dialog.Handle={dialog.Handle}");
        TestOutputHelper.WriteLine($"Focus={PInvoke.GetFocus()} Active={PInvoke.GetActiveWindow()} Foreground={PInvoke.GetForegroundWindow()} Capture={PInvoke.GetCapture()}");
        TestOutputHelper.WriteLine($"OpenForms={Application.OpenForms.Count}");
        foreach (Form openForm in Application.OpenForms)
        {
            TestOutputHelper.WriteLine($"OpenForm: Name='{openForm.Name}' Text='{openForm.Text}' Visible={openForm.Visible} Handle={openForm.Handle}");
        }

        throw new TimeoutException($"WinFormsX UI test timed out after {s_winFormsXTestTimeout.TotalSeconds:F0}s.");
    }
}

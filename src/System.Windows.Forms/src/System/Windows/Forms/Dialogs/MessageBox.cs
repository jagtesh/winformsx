// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms.Primitives;

namespace System.Windows.Forms;

/// <summary>
///  Displays a message box that can contain text, buttons, and symbols that inform and instruct the user.
/// </summary>
public class MessageBox
{
    [ThreadStatic]
    private static HelpInfo[]? t_helpInfoTable;

    // This is meant to be a static class, but predates that feature.
    private MessageBox()
    {
    }

    internal static HelpInfo? HelpInfo
    {
        get
        {
            // Unfortunately, there's no easy way to obtain handle of a message box.
            // We'll have to rely on the fact that modal message loops have to pop off in an orderly way.

            if (t_helpInfoTable is not null && t_helpInfoTable.Length > 0)
            {
                // The top of the stack is actually at the end of the array.
                return t_helpInfoTable[^1];
            }

            return null;
        }
    }

    private static MESSAGEBOX_STYLE GetMessageBoxStyle(
        IWin32Window? owner,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        bool showHelp)
    {
        SourceGenerated.EnumValidator.Validate(buttons, nameof(buttons));
        SourceGenerated.EnumValidator.Validate(icon, nameof(icon));
        SourceGenerated.EnumValidator.Validate(defaultButton, nameof(defaultButton));

        // options intentionally not verified because we don't expose all the options Win32 supports.

        if (!SystemInformation.UserInteractive && (options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) == 0)
        {
            throw new InvalidOperationException(SR.CantShowModalOnNonInteractive);
        }

        if (owner is not null && (options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) != 0)
        {
            throw new ArgumentException(SR.CantShowMBServiceWithOwner, nameof(options));
        }

        if (showHelp && (options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) != 0)
        {
            throw new ArgumentException(SR.CantShowMBServiceWithHelp, nameof(options));
        }

        MESSAGEBOX_STYLE style = (showHelp) ? MESSAGEBOX_STYLE.MB_HELP : 0;
        style |= (MESSAGEBOX_STYLE)buttons | (MESSAGEBOX_STYLE)icon | (MESSAGEBOX_STYLE)defaultButton | (MESSAGEBOX_STYLE)options;
        return style;
    }

    private static void PopHelpInfo()
    {
        // we roll our own stack here because we want a pretty lightweight implementation.
        // usually there's only going to be one message box shown at a time.  But if
        // someone shows two message boxes (say by launching them via a WM_TIMER message)
        // we've got to gracefully handle the current help info.
        if (t_helpInfoTable is null)
        {
            Debug.Fail("Why are we being called when there's nothing to pop?");
        }
        else
        {
            if (t_helpInfoTable.Length == 1)
            {
                t_helpInfoTable = null;
            }
            else
            {
                int newCount = t_helpInfoTable.Length - 1;
                HelpInfo[] newTable = new HelpInfo[newCount];
                Array.Copy(t_helpInfoTable, newTable, newCount);
                t_helpInfoTable = newTable;
            }
        }
    }

    private static void PushHelpInfo(HelpInfo hpi)
    {
        // we roll our own stack here because we want a pretty lightweight implementation.
        // usually there's only going to be one message box shown at a time.  But if
        // someone shows two message boxes (say by launching them via a WM_TIMER message)
        // we've got to gracefully handle the current help info.

        int lastCount = 0;
        HelpInfo[] newTable;

        if (t_helpInfoTable is null)
        {
            newTable = new HelpInfo[lastCount + 1];
        }
        else
        {
            // if we already have a table - allocate a new slot
            lastCount = t_helpInfoTable.Length;
            newTable = new HelpInfo[lastCount + 1];
            Array.Copy(t_helpInfoTable, newTable, lastCount);
        }

        newTable[lastCount] = hpi;
        t_helpInfoTable = newTable;
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style with Help Button.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        bool displayHelpButton)
    {
        return ShowCore(null, text, caption, buttons, icon, defaultButton, options, displayHelpButton);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style and Help file Path .
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath)
    {
        HelpInfo hpi = new(helpFilePath);
        return ShowCore(null, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style and Help file Path for a IWin32Window.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath)
    {
        HelpInfo hpi = new(helpFilePath);
        return ShowCore(owner, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style, Help file Path and keyword.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath,
        string keyword)
    {
        HelpInfo hpi = new(helpFilePath, keyword);
        return ShowCore(null, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style, Help file Path and keyword for a IWin32Window.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath,
        string keyword)
    {
        HelpInfo hpi = new(helpFilePath, keyword);
        return ShowCore(owner, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style, Help file Path and HelpNavigator.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath,
        HelpNavigator navigator)
    {
        HelpInfo hpi = new(helpFilePath, navigator);
        return ShowCore(null, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style, Help file Path and HelpNavigator for IWin32Window.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath,
        HelpNavigator navigator)
    {
        HelpInfo hpi = new(helpFilePath, navigator);
        return ShowCore(owner, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style, Help file Path ,HelpNavigator and object.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath,
        HelpNavigator navigator,
        object? param)
    {
        HelpInfo hpi = new(helpFilePath, navigator, param);

        return ShowCore(null, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, style, Help file Path ,HelpNavigator and object for a IWin32Window.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        string helpFilePath,
        HelpNavigator navigator,
        object? param)
    {
        HelpInfo hpi = new(helpFilePath, navigator, param);

        return ShowCore(owner, text, caption, buttons, icon, defaultButton, options, hpi);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options)
    {
        return ShowCore(null, text, caption, buttons, icon, defaultButton, options, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton)
    {
        return ShowCore(null, text, caption, buttons, icon, defaultButton, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return ShowCore(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons)
    {
        return ShowCore(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text and caption.
    /// </summary>
    public static DialogResult Show(string? text, string? caption)
    {
        return ShowCore(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text.
    /// </summary>
    public static DialogResult Show(string? text)
    {
        return ShowCore(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options)
    {
        return ShowCore(owner, text, caption, buttons, icon, defaultButton, options, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton)
    {
        return ShowCore(owner, text, caption, buttons, icon, defaultButton, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return ShowCore(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text, caption, and style.
    /// </summary>
    public static DialogResult Show(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons)
    {
        return ShowCore(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text and caption.
    /// </summary>
    public static DialogResult Show(IWin32Window? owner, string? text, string? caption)
    {
        return ShowCore(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0, false);
    }

    /// <summary>
    ///  Displays a message box with specified text.
    /// </summary>
    public static DialogResult Show(IWin32Window? owner, string? text)
    {
        return ShowCore(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0, false);
    }

    private static DialogResult ShowCore(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        HelpInfo hpi)
    {
        DialogResult result = DialogResult.None;
        try
        {
            PushHelpInfo(hpi);
            result = ShowCore(owner, text, caption, buttons, icon, defaultButton, options, true);
        }
        finally
        {
            PopHelpInfo();
        }

        return result;
    }

    private static DialogResult ShowCore(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton,
        MessageBoxOptions options,
        bool showHelp)
    {
        if (LocalAppContextSwitches.NoClientNotifications)
        {
            return DialogResult.None;
        }

        MESSAGEBOX_STYLE style = GetMessageBoxStyle(owner, buttons, icon, defaultButton, options, showHelp);
        if (System.Drawing.Graphics.IsBackendActive)
        {
            return ShowManagedMessageBox(owner, text, caption, buttons, icon, defaultButton);
        }

        HandleRef<HWND> handle = default;
        if (showHelp || ((options & (MessageBoxOptions.ServiceNotification | MessageBoxOptions.DefaultDesktopOnly)) == 0))
        {
            handle = owner is null ? Control.GetHandleRef(PInvoke.GetActiveWindow()) : Control.GetSafeHandle(owner);
        }

        if (Application.UseVisualStyles)
        {
            // CLR4.0 or later, shell32.dll needs to be loaded explicitly.
            if (PInvoke.GetModuleHandle(Libraries.Shell32) == 0)
            {
                if (PInvoke.LoadLibraryFromSystemPathIfAvailable(Libraries.Shell32) == HINSTANCE.Null)
                {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    throw new Win32Exception(lastWin32Error, string.Format(SR.LoadDLLError, Libraries.Shell32));
                }
            }
        }

        // Activate theming scope to get theming for controls at design time and when hosted in browser.
        // NOTE: If a theming context is already active, this call is very fast, so shouldn't be a perf issue.
        using ThemingScope scope = new(Application.UseVisualStyles);

        Application.BeginModalMessageLoop();
        try
        {
            return (DialogResult)PInvoke.MessageBox(handle.Handle, text, caption, style);
        }
        finally
        {
            Application.EndModalMessageLoop();

            // Right after the dialog box is closed, Windows sends WM_SETFOCUS back to the previously active control
            // but since we have disabled this thread main window the message is lost. So we have to send it again after
            // we enable the main window.
            PInvoke.SendMessage(handle, PInvoke.WM_SETFOCUS);
        }
    }

    private static DialogResult ShowManagedMessageBox(
        IWin32Window? owner,
        string? text,
        string? caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton)
    {
        using ManagedMessageBoxForm form = new(owner?.Handle ?? IntPtr.Zero, text, caption, buttons, icon, defaultButton);
        return form.ShowDialog(owner);
    }

    private static DialogResult GetBackendDefaultDialogResult(MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton)
    {
        DialogResult[] results = buttons switch
        {
            MessageBoxButtons.OK => [DialogResult.OK],
            MessageBoxButtons.OKCancel => [DialogResult.OK, DialogResult.Cancel],
            MessageBoxButtons.AbortRetryIgnore => [DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore],
            MessageBoxButtons.YesNoCancel => [DialogResult.Yes, DialogResult.No, DialogResult.Cancel],
            MessageBoxButtons.YesNo => [DialogResult.Yes, DialogResult.No],
            MessageBoxButtons.RetryCancel => [DialogResult.Retry, DialogResult.Cancel],
            MessageBoxButtons.CancelTryContinue => [DialogResult.Cancel, DialogResult.TryAgain, DialogResult.Continue],
            _ => [DialogResult.OK],
        };

        int index = defaultButton switch
        {
            MessageBoxDefaultButton.Button2 => 1,
            MessageBoxDefaultButton.Button3 => 2,
            MessageBoxDefaultButton.Button4 => 3,
            _ => 0,
        };

        return results[Math.Clamp(index, 0, results.Length - 1)];
    }

    private sealed class ManagedMessageBoxForm : Form
    {
        private readonly nint _owner;
        private readonly DialogResult _defaultResult;

        public ManagedMessageBoxForm(
            nint owner,
            string? text,
            string? caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton)
        {
            _owner = owner;
            _defaultResult = GetBackendDefaultDialogResult(buttons, defaultButton);

            Text = string.IsNullOrEmpty(caption) ? "Message" : caption;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(420, 160);
            MinimumSize = new Size(320, 150);

            Controls.Add(CreateContent(text, icon, buttons, defaultButton));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            BeginInvoke(() =>
            {
                if (_owner != 0 && Handle != IntPtr.Zero)
                {
                    PInvoke.PostMessage((HWND)_owner, PInvoke.WM_ENTERIDLE, (WPARAM)0, (LPARAM)Handle);
                }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.None)
            {
                DialogResult = GetCancelDialogResult() ?? _defaultResult;
            }

            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.MsgInternal == PInvoke.WM_COMMAND)
            {
                DialogResult result = (MESSAGEBOX_RESULT)(int)m.WParamInternal.LOWORD switch
                {
                    MESSAGEBOX_RESULT.IDOK => DialogResult.OK,
                    MESSAGEBOX_RESULT.IDCANCEL => DialogResult.Cancel,
                    MESSAGEBOX_RESULT.IDABORT => DialogResult.Abort,
                    MESSAGEBOX_RESULT.IDRETRY => DialogResult.Retry,
                    MESSAGEBOX_RESULT.IDIGNORE => DialogResult.Ignore,
                    MESSAGEBOX_RESULT.IDYES => DialogResult.Yes,
                    MESSAGEBOX_RESULT.IDNO => DialogResult.No,
                    MESSAGEBOX_RESULT.IDTRYAGAIN => DialogResult.TryAgain,
                    MESSAGEBOX_RESULT.IDCONTINUE => DialogResult.Continue,
                    _ => DialogResult.None
                };

                if (result != DialogResult.None)
                {
                    DialogResult = result;
                    Close();
                    return;
                }
            }

            base.WndProc(ref m);
        }

        private TableLayoutPanel CreateContent(
            string? text,
            MessageBoxIcon icon,
            MessageBoxButtons buttons,
            MessageBoxDefaultButton defaultButton)
        {
            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12)
            };

            root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label iconLabel = new()
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 22.0f, FontStyle.Bold),
                Padding = new Padding(0, 0, 14, 0),
                Text = GetIconText(icon)
            };
            root.Controls.Add(iconLabel, 0, 0);

            Label message = new()
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                Text = text ?? string.Empty,
                TextAlign = ContentAlignment.MiddleLeft
            };
            root.Controls.Add(message, 1, 0);

            FlowLayoutPanel buttonPanel = new()
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.SetColumnSpan(buttonPanel, 2);
            root.Controls.Add(buttonPanel, 0, 1);

            Button? defaultButtonControl = null;
            Button? cancelButtonControl = null;
            (string Label, DialogResult Result)[] buttonDefinitions = GetButtons(buttons);
            for (int i = buttonDefinitions.Length - 1; i >= 0; i--)
            {
                (string label, DialogResult result) = buttonDefinitions[i];
                Button button = new()
                {
                    AutoSize = true,
                    DialogResult = result,
                    MinimumSize = new Size(82, 28),
                    Text = label
                };

                button.Click += (sender, e) =>
                {
                    DialogResult = ((Button)sender!).DialogResult;
                    Close();
                };

                buttonPanel.Controls.Add(button);
                if (result == _defaultResult)
                {
                    defaultButtonControl = button;
                }

                if (result == DialogResult.Cancel)
                {
                    cancelButtonControl = button;
                }
            }

            AcceptButton = defaultButtonControl ?? buttonPanel.Controls.OfType<Button>().FirstOrDefault();
            CancelButton = cancelButtonControl;
            return root;
        }

        private DialogResult? GetCancelDialogResult()
        {
            return GetButtonsFromControls().Any(static result => result == DialogResult.Cancel)
                ? DialogResult.Cancel
                : null;
        }

        private IEnumerable<DialogResult> GetButtonsFromControls()
        {
            foreach (Button button in Controls.Find(string.Empty, searchAllChildren: true).OfType<Button>())
            {
                yield return button.DialogResult;
            }
        }

        private static string GetIconText(MessageBoxIcon icon)
            => icon switch
            {
                MessageBoxIcon.Error => "X",
                MessageBoxIcon.Question => "?",
                MessageBoxIcon.Warning => "!",
                MessageBoxIcon.Information => "i",
                _ => string.Empty
            };

        private static (string Label, DialogResult Result)[] GetButtons(MessageBoxButtons buttons)
            => buttons switch
            {
                MessageBoxButtons.OK => [("OK", DialogResult.OK)],
                MessageBoxButtons.OKCancel => [("OK", DialogResult.OK), ("Cancel", DialogResult.Cancel)],
                MessageBoxButtons.AbortRetryIgnore => [("Abort", DialogResult.Abort), ("Retry", DialogResult.Retry), ("Ignore", DialogResult.Ignore)],
                MessageBoxButtons.YesNoCancel => [("Yes", DialogResult.Yes), ("No", DialogResult.No), ("Cancel", DialogResult.Cancel)],
                MessageBoxButtons.YesNo => [("Yes", DialogResult.Yes), ("No", DialogResult.No)],
                MessageBoxButtons.RetryCancel => [("Retry", DialogResult.Retry), ("Cancel", DialogResult.Cancel)],
                MessageBoxButtons.CancelTryContinue => [("Cancel", DialogResult.Cancel), ("Try Again", DialogResult.TryAgain), ("Continue", DialogResult.Continue)],
                _ => [("OK", DialogResult.OK)]
            };
    }
}

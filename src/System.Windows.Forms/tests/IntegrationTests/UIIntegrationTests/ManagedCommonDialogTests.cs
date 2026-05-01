// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Windows.Forms.Platform;
using Windows.Win32.UI.WindowsAndMessaging;
using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

public class ManagedCommonDialogTests : ControlTestBase
{
    public ManagedCommonDialogTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [UIFact]
    public void SaveFileDialog_ShowDialog_Cancel_Success()
    {
        using DialogHostForm dialogOwnerForm = new();
        using SaveFileDialog dialog = new()
        {
            InitialDirectory = Path.GetTempPath()
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void SaveFileDialog_ShowDialog_Accept_Success()
    {
        string fileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        using AcceptDialogForm dialogOwnerForm = new();
        using SaveFileDialog dialog = new()
        {
            InitialDirectory = Path.GetDirectoryName(fileName),
            FileName = fileName,
            OverwritePrompt = false
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(fileName, dialog.FileName);
    }

    [UIFact]
    public void SaveFileDialog_ShowDialog_OverwritePromptAccepts_Success()
    {
        string fileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(fileName, "existing");
        try
        {
            using PromptAcceptDialogForm dialogOwnerForm = new();
            using SaveFileDialog dialog = new()
            {
                InitialDirectory = Path.GetDirectoryName(fileName),
                FileName = fileName,
                OverwritePrompt = true
            };

            Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
            Assert.Equal(fileName, dialog.FileName);
        }
        finally
        {
            File.Delete(fileName);
        }
    }

    [UIFact]
    public void SaveFileDialog_ShowDialog_CreatePromptAccepts_Success()
    {
        string fileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        using PromptAcceptDialogForm dialogOwnerForm = new();
        using SaveFileDialog dialog = new()
        {
            CreatePrompt = true,
            InitialDirectory = Path.GetDirectoryName(fileName),
            FileName = fileName,
            OverwritePrompt = false
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(fileName, dialog.FileName);
    }

    [UIFact]
    public void OpenFileDialog_ShowDialog_MissingFilePromptCancels_Success()
    {
        string fileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        using PromptAcceptDialogForm dialogOwnerForm = new();
        using OpenFileDialog dialog = new()
        {
            InitialDirectory = Path.GetDirectoryName(fileName),
            FileName = fileName
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void ColorDialog_ShowDialog_Accept_Success()
    {
        using AcceptDialogForm dialogOwnerForm = new();
        using ColorDialog dialog = new()
        {
            Color = Color.CornflowerBlue
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(Color.CornflowerBlue.ToArgb(), dialog.Color.ToArgb());
    }

    [UIFact]
    public void ColorDialog_ShowDialog_SelectCustomColor_Success()
    {
        Color customColor = Color.FromArgb(12, 34, 56);
        using ColorSelectionDialogForm dialogOwnerForm = new(customColor);
        using ColorDialog dialog = new()
        {
            CustomColors = [ColorTranslator.ToWin32(customColor)],
            FullOpen = true
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(customColor.ToArgb(), dialog.Color.ToArgb());
    }

    [UIFact]
    public void FontDialog_ShowDialog_Accept_Success()
    {
        using AcceptDialogForm dialogOwnerForm = new();
        using Font selectedFont = new(FontFamily.GenericSansSerif, 14.0f);
        using FontDialog dialog = new()
        {
            Font = selectedFont
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(selectedFont.FontFamily.Name, dialog.Font.FontFamily.Name);
        Assert.Equal(selectedFont.Size, dialog.Font.Size);
    }

    [UIFact]
    public void FontDialog_ShowDialog_SelectEffects_Success()
    {
        using FontEffectsDialogForm dialogOwnerForm = new();
        using Font selectedFont = new(FontFamily.GenericSansSerif, 14.0f);
        using FontDialog dialog = new()
        {
            Font = selectedFont,
            ShowEffects = true
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.True(dialog.Font.Bold);
        Assert.True(dialog.Font.Underline);
    }

    [UIFact]
    public void FontDialog_ShowDialog_SelectColor_Success()
    {
        Color selectedColor = Color.Magenta;
        using FontColorDialogForm dialogOwnerForm = new(selectedColor);
        using Font selectedFont = new(FontFamily.GenericSansSerif, 14.0f);
        using FontDialog dialog = new()
        {
            Font = selectedFont,
            ShowColor = true
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(selectedColor.ToArgb(), dialog.Color.ToArgb());
    }

    [UIFact]
    public void OwnedTopLevelWindow_WithOwner_IsNotTreatedAsChild()
    {
        ImpellerWindowInterop windowInterop = Assert.IsType<ImpellerWindowInterop>(PlatformApi.Window);
        HWND owner = windowInterop.CreateWindowEx(
            default,
            "WinFormsXTestOwner",
            "Owner",
            WINDOW_STYLE.WS_OVERLAPPEDWINDOW,
            100,
            100,
            640,
            480,
            HWND.Null,
            HMENU.Null,
            HINSTANCE.Null,
            null);
        HWND dialog = windowInterop.CreateWindowEx(
            default,
            "WinFormsXTestDialog",
            "Dialog",
            WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_SYSMENU,
            250,
            260,
            320,
            200,
            owner,
            HMENU.Null,
            HINSTANCE.Null,
            null);

        try
        {
            Assert.Equal(owner, windowInterop.GetParent(dialog));
            Assert.False(windowInterop.IsChild(owner, dialog));

            Assert.True(windowInterop.GetWindowRect(dialog, out RECT dialogRect));
            Assert.Equal(250, dialogRect.left);
            Assert.Equal(260, dialogRect.top);
        }
        finally
        {
            windowInterop.DestroyWindow(dialog);
            windowInterop.DestroyWindow(owner);
        }
    }

    [UIFact]
    public void FileDialog_FilterPatterns_SelectsRequestedFilterIndex()
    {
        Assert.Equal(["*.png", "*.jpg"], ImpellerDialogInterop.GetFilterPatterns("Images|*.png;*.jpg|Text|*.txt", 1));
        Assert.Equal(["*.txt"], ImpellerDialogInterop.GetFilterPatterns("Images|*.png;*.jpg|Text|*.txt", 2));
    }

    [UIFact]
    public void FileDialog_MatchesFilter_AppliesWildcardPatterns()
    {
        Assert.True(ImpellerDialogInterop.MatchesFilter("report.TXT", ["*.txt"]));
        Assert.True(ImpellerDialogInterop.MatchesFilter("archive.tar.gz", ["*.tar.?z"]));
        Assert.False(ImpellerDialogInterop.MatchesFilter("image.png", ["*.txt"]));
    }

    private class AcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Accept(dialogHandle);
        }
    }

    private class FontEffectsDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            if (Control.FromHandle(dialogHandle) is Form dialog)
            {
                foreach (CheckBox checkBox in FindControls<CheckBox>(dialog))
                {
                    if (checkBox.Text is "Bold" or "Underline")
                    {
                        checkBox.Checked = true;
                    }
                }
            }

            Accept(dialogHandle);
        }

        private static IEnumerable<T> FindControls<T>(Control parent)
            where T : Control
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T nested in FindControls<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private class ColorSelectionDialogForm(Color color) : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            if (Control.FromHandle(dialogHandle) is Form dialog)
            {
                foreach (ListBox listBox in FindControls<ListBox>(dialog))
                {
                    for (int i = 0; i < listBox.Items.Count; i++)
                    {
                        if (listBox.Items[i] is Color itemColor && itemColor.ToArgb() == color.ToArgb())
                        {
                            listBox.SelectedIndex = i;
                            Accept(dialogHandle);
                            return;
                        }
                    }
                }
            }

            Accept(dialogHandle);
        }

        private static IEnumerable<T> FindControls<T>(Control parent)
            where T : Control
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T nested in FindControls<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private class FontColorDialogForm(Color color) : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            if (Control.FromHandle(dialogHandle) is Form dialog)
            {
                foreach (ComboBox comboBox in FindControls<ComboBox>(dialog))
                {
                    for (int i = 0; i < comboBox.Items.Count; i++)
                    {
                        if (comboBox.Items[i] is Color itemColor && itemColor.ToArgb() == color.ToArgb())
                        {
                            comboBox.SelectedIndex = i;
                            Accept(dialogHandle);
                            return;
                        }
                    }
                }
            }

            Accept(dialogHandle);
        }

        private static IEnumerable<T> FindControls<T>(Control parent)
            where T : Control
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T nested in FindControls<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private class PromptAcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            if (Control.FromHandle(dialogHandle) is Form dialog
                && FindControls<Button>(dialog).Any(button => button.Text == "Yes"))
            {
                PInvoke.SendMessage(dialogHandle, PInvoke.WM_COMMAND, (WPARAM)(nint)MESSAGEBOX_RESULT.IDYES);
                return;
            }

            Accept(dialogHandle);
        }

        private static IEnumerable<T> FindControls<T>(Control parent)
            where T : Control
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T nested in FindControls<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }
}

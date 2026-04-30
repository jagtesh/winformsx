// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

public class OpenFileDialogTests : ControlTestBase
{
    public OpenFileDialogTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    // Regression test for https://github.com/dotnet/winforms/issues/8108
    [UIFact]
    public void OpenFileDialogTests_OpenWithNonExistingInitDirectory_Success()
    {
        using DialogHostForm dialogOwnerForm = new();
        using OpenFileDialog dialog = new();
        dialog.InitialDirectory = Guid.NewGuid().ToString();
        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void OpenFileDialogTests_OpenWithExistingInitDirectory_Success()
    {
        using DialogHostForm dialogOwnerForm = new();
        using OpenFileDialog dialog = new();
        dialog.InitialDirectory = Path.GetTempPath();
        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    // Regression test for https://github.com/dotnet/winforms/issues/8414
    [UIFact]
    public void OpenFileDialogTests_ResultWithMultiselect()
    {
        using var tempFile = TempFile.Create(0);
        using AcceptDialogForm dialogOwnerForm = new();
        using OpenFileDialog dialog = new();
        dialog.Multiselect = true;
        dialog.InitialDirectory = Path.GetDirectoryName(tempFile.Path);
        dialog.FileName = tempFile.Path;
        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(tempFile.Path, dialog.FileName);
    }

    [UIFact]
    public void OpenFileDialogTests_ManagedPicker_MultiselectAppliesFilter()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        string first = Path.Combine(tempDirectory, "alpha.txt");
        string second = Path.Combine(tempDirectory, "beta.txt");
        string ignored = Path.Combine(tempDirectory, "image.png");

        try
        {
            File.WriteAllText(first, "alpha");
            File.WriteAllText(second, "beta");
            File.WriteAllText(ignored, "image");

            using SelectAllFilesDialogForm dialogOwnerForm = new();
            using OpenFileDialog dialog = new()
            {
                InitialDirectory = tempDirectory,
                Filter = "Text|*.txt|Images|*.png",
                FilterIndex = 1,
                Multiselect = true
            };

            Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
            Assert.Equal([first, second], dialog.FileNames);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private class AcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Accept(dialogHandle);
        }
    }

    private class SelectAllFilesDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            if (Control.FromHandle(dialogHandle) is Form dialog)
            {
                ListBox listBox = FindControls<ListBox>(dialog).Single();
                for (int i = 0; i < listBox.Items.Count; i++)
                {
                    listBox.SetSelected(i, value: true);
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
}

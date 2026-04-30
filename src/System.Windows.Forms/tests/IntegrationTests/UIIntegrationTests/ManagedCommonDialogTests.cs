// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
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

    private class AcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Accept(dialogHandle);
        }
    }
}

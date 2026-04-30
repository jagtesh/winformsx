// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.UITests;

using System.Drawing;
using Xunit.Abstractions;

public class MessageBoxTests : ControlTestBase
{
    public MessageBoxTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [UIFact]
    public void MessageBox_MessageBoxDialogResult_Valid()
    {
        using NoClientNotificationsScope scope = new(enable: true);
        Assert.Equal(DialogResult.None, MessageBox.Show("Testing DialogResult"));
    }

    [UIFact]
    public void MessageBox_ShowWithOwner_Accept_Success()
    {
        using AcceptDialogForm dialogOwnerForm = new();

        Assert.Equal(
            DialogResult.OK,
            MessageBox.Show(
                dialogOwnerForm,
                "Testing DialogResult",
                "Caption",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information));
    }

    [UIFact]
    public void MessageBox_ShowWithOwner_Close_Success()
    {
        using DialogHostForm dialogOwnerForm = new();

        Assert.Equal(
            DialogResult.Cancel,
            MessageBox.Show(
                dialogOwnerForm,
                "Testing DialogResult",
                "Caption",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning));
    }

    [UIFact]
    public void MessageBox_ShowWithOwner_Icon_RendersImage_Success()
    {
        using IconDialogForm dialogOwnerForm = new();

        Assert.Equal(
            DialogResult.OK,
            MessageBox.Show(
                dialogOwnerForm,
                "Testing icon",
                "Caption",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Error));
    }

    [UIFact]
    public void MessageBox_ShowWithOwner_HelpButton_RaisesHelpRequested_Success()
    {
        bool helpRequested = false;
        using HelpButtonDialogForm dialogOwnerForm = new();
        dialogOwnerForm.HelpRequested += (sender, e) =>
        {
            helpRequested = true;
            e.Handled = true;
        };

        Assert.Equal(
            DialogResult.OK,
            MessageBox.Show(
                dialogOwnerForm,
                "Testing help",
                "Caption",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                0,
                "help.chm"));
        Assert.True(helpRequested);
    }

    private class AcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Accept(dialogHandle);
        }
    }

    private class IconDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Control dialog = Control.FromHandle((IntPtr)dialogHandle)!;
            PictureBox pictureBox = FindDescendant<PictureBox>(dialog)!;
            Assert.NotNull(pictureBox);
            Assert.NotNull(pictureBox.Image);
            Assert.Equal(new Size(32, 32), pictureBox.Size);
            Accept(dialogHandle);
        }
    }

    private class HelpButtonDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Control dialog = Control.FromHandle((IntPtr)dialogHandle)!;
            Button helpButton = FindDescendant<Button>(dialog, static button => button.Text == "Help")!;
            Assert.NotNull(helpButton);
            helpButton.PerformClick();
            Accept(dialogHandle);
        }
    }

    private static T? FindDescendant<T>(Control root, Func<T, bool>? predicate = null)
        where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T typed && (predicate is null || predicate(typed)))
            {
                return typed;
            }

            T? descendant = FindDescendant(child, predicate);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}

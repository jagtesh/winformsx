// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.UITests;

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

    private class AcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Accept(dialogHandle);
        }
    }
}

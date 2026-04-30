// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Printing;
using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

public class PageSetupDialogUITests : ControlTestBase
{
    public PageSetupDialogUITests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [UIFact]
    public void PageSetupDialog_ShowDialog_Cancel_Success()
    {
        using DialogHostForm dialogOwnerForm = new();
        using PrintDocument document = new();
        using PageSetupDialog dialog = new()
        {
            Document = document
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(dialogOwnerForm));
    }

    [UIFact]
    public void PageSetupDialog_ShowDialog_Accept_Success()
    {
        using AcceptDialogForm dialogOwnerForm = new();
        using PrintDocument document = new();
        document.DefaultPageSettings.Margins = new Margins(100, 110, 120, 130);
        document.DefaultPageSettings.Landscape = false;

        using PageSetupDialog dialog = new()
        {
            Document = document
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(100, document.DefaultPageSettings.Margins.Left);
        Assert.Equal(110, document.DefaultPageSettings.Margins.Right);
        Assert.Equal(120, document.DefaultPageSettings.Margins.Top);
        Assert.Equal(130, document.DefaultPageSettings.Margins.Bottom);
        Assert.False(document.DefaultPageSettings.Landscape);
    }

    [UIFact]
    public void PageSetupDialog_ShowDialog_MinMargins_ClampsInitialValues()
    {
        using AcceptDialogForm dialogOwnerForm = new();
        using PrintDocument document = new();
        document.DefaultPageSettings.Margins = new Margins(10, 20, 30, 40);

        using PageSetupDialog dialog = new()
        {
            Document = document,
            MinMargins = new Margins(50, 60, 70, 80)
        };

        Assert.Equal(DialogResult.OK, dialog.ShowDialog(dialogOwnerForm));
        Assert.Equal(50, document.DefaultPageSettings.Margins.Left);
        Assert.Equal(60, document.DefaultPageSettings.Margins.Right);
        Assert.Equal(70, document.DefaultPageSettings.Margins.Top);
        Assert.Equal(80, document.DefaultPageSettings.Margins.Bottom);
    }

    private class AcceptDialogForm : DialogHostForm
    {
        protected override void OnDialogIdle(HWND dialogHandle)
        {
            Accept(dialogHandle);
        }
    }
}

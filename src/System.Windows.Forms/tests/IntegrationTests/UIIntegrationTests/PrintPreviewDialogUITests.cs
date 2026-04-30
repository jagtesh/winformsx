// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Drawing.Printing;
using Xunit.Abstractions;

namespace System.Windows.Forms.UITests;

public class PrintPreviewDialogUITests : ControlTestBase
{
    public PrintPreviewDialogUITests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
    }

    [UIFact]
    public void PreviewPrintController_Print_RendersPageToBitmap()
    {
        bool printed = false;
        PreviewPrintController controller = new();
        using PrintDocument document = new();
        document.PrintController = controller;
        document.PrintPage += (sender, e) =>
        {
            printed = true;
            e.Graphics!.DrawRectangle(Pens.Black, 10, 10, 50, 50);
            e.HasMorePages = false;
        };

        document.Print();

        PreviewPageInfo page = Assert.Single(controller.GetPreviewPageInfo());
        Assert.True(printed);
        Assert.IsType<Bitmap>(page.Image);
        Assert.NotEqual(Size.Empty, page.PhysicalSize);
    }

    [UIFact]
    public void PrintPreviewDialog_ShowDialog_CanCloseFromShown()
    {
        bool printed = false;
        using PrintDocument document = new();
        document.PrintPage += (sender, e) =>
        {
            printed = true;
            e.Graphics!.DrawRectangle(Pens.Black, 10, 10, 50, 50);
            e.HasMorePages = false;
        };

        using PrintPreviewDialog dialog = new()
        {
            Document = document
        };

        dialog.Shown += (sender, e) =>
        {
            dialog.PrintPreviewControl.Refresh();
            dialog.BeginInvoke(() =>
            {
                DateTime timeout = DateTime.UtcNow.AddSeconds(5);
                while (!printed && DateTime.UtcNow < timeout)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }

                dialog.Close();
            });
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
        Assert.True(printed);
    }
}

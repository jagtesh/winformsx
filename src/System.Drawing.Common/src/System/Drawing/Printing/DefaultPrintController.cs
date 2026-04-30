// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Printing;

/// <summary>
///  Specifies a print controller that sends information to a printer.
/// </summary>
public class StandardPrintController : PrintController
{
    private Bitmap? _pageImage;
    private Graphics? _pageGraphics;

    /// <summary>
    ///  Implements StartPrint for printing to a physical printer.
    /// </summary>
    public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnStartPrint(document, e);
    }

    /// <summary>
    ///  Implements StartPage for printing to a physical printer.
    /// </summary>
    public override Graphics OnStartPage(PrintDocument document, PrintPageEventArgs e)
    {
        DisposeCurrentPage();

        Rectangle pageBounds = e.PageBounds;
        int width = Math.Max(1, pageBounds.Width);
        int height = Math.Max(1, pageBounds.Height);

        _pageImage = new Bitmap(width, height);
        _pageGraphics = Graphics.FromImage(_pageImage);
        return _pageGraphics;
    }

    /// <summary>
    ///  Implements EndPage for printing to a physical printer.
    /// </summary>
    public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
        base.OnEndPage(document, e);
        DisposeCurrentPage();
    }

    /// <summary>
    ///  Implements EndPrint for printing to a physical printer.
    /// </summary>
    public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
        DisposeCurrentPage();
        base.OnEndPrint(document, e);
    }

    private void DisposeCurrentPage()
    {
        _pageGraphics?.Dispose();
        _pageGraphics = null;

        _pageImage?.Dispose();
        _pageImage = null;
    }
}

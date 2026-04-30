// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Printing;

/// <summary>
///  A PrintController which "prints" to a series of images.
/// </summary>
public class PreviewPrintController : PrintController
{
    private Graphics? _graphics;
    private readonly List<PreviewPageInfo> _list = [];

    public override bool IsPreview => true;

    public virtual bool UseAntiAlias { get; set; }

    public PreviewPageInfo[] GetPreviewPageInfo() => [.. _list];

    /// <summary>
    ///  Implements StartPrint for generating print preview information.
    /// </summary>
    public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnStartPrint(document, e);

        if (!document.PrinterSettings.IsValid)
        {
            throw new InvalidPrinterException(document.PrinterSettings);
        }
    }

    /// <summary>
    ///  Implements StartEnd for generating print preview information.
    /// </summary>
    public override Graphics OnStartPage(PrintDocument document, PrintPageEventArgs e)
    {
        base.OnStartPage(document, e);

        if (e.CopySettingsToDevMode)
        {
            e.PageSettings.CopyToHdevmode(_modeHandle!);
        }

        Size size = e.PageBounds.Size;

        Bitmap bitmap = new(Math.Max(1, size.Width), Math.Max(1, size.Height));
        PreviewPageInfo info = new(bitmap, size);
        _list.Add(info);
        PrintPreviewGraphics printGraphics = new(document, e);
        _graphics = Graphics.FromImage(bitmap);

        if (document.OriginAtMargins)
        {
            // Adjust the origin of the graphics object to be at the
            // user-specified margin location
            const int dpiX = 96;
            const int dpiY = 96;
            const int hardMarginX_DU = 0;
            const int hardMarginY_DU = 0;
            float hardMarginX = hardMarginX_DU * 100f / dpiX;
            float hardMarginY = hardMarginY_DU * 100f / dpiY;

            _graphics.TranslateTransform(-hardMarginX, -hardMarginY);
            _graphics.TranslateTransform(document.DefaultPageSettings.Margins.Left, document.DefaultPageSettings.Margins.Top);
        }

        _graphics.PrintingHelper = printGraphics;

        if (UseAntiAlias)
        {
            _graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            _graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        }

        return _graphics;
    }

    public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
        _graphics?.Dispose();
        _graphics = null;
        base.OnEndPage(document, e);
    }

    public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnEndPrint(document, e);
    }
}

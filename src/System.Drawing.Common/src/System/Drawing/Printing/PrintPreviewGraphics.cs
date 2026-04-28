// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Printing;

namespace System.Drawing;

/// <summary>
///  Retrieves the printer graphics during preview.
/// </summary>
internal sealed class PrintPreviewGraphics
{
    private readonly PrintPageEventArgs _printPageEventArgs;
    private readonly PrintDocument _printDocument;

    public PrintPreviewGraphics(PrintDocument document, PrintPageEventArgs e)
    {
        _printPageEventArgs = e;
        _printDocument = document;
    }

    /// <summary>
    ///  Gets the Visible bounds of this graphics object. Used during print preview.
    /// </summary>
    public RectangleF VisibleClipBounds
    {
        get
        {
            HGLOBAL hdevmode = _printPageEventArgs.PageSettings.PrinterSettings.GetHdevmodeInternal();

            using var hdc = _printPageEventArgs.PageSettings.PrinterSettings.CreateDeviceContext(hdevmode);
            using Graphics graphics = Graphics.FromHdcInternal(hdc);

            if (_printDocument.OriginAtMargins)
            {
                // Adjust the origin of the graphics object to be at the user-specified margin location
                const int dpiX = 96;
                const int dpiY = 96;
                const int hardMarginX_DU = 0;
                const int hardMarginY_DU = 0;
                float hardMarginX = hardMarginX_DU * 100 / dpiX;
                float hardMarginY = hardMarginY_DU * 100 / dpiY;

                graphics.TranslateTransform(-hardMarginX, -hardMarginY);
                graphics.TranslateTransform(_printDocument.DefaultPageSettings.Margins.Left, _printDocument.DefaultPageSettings.Margins.Top);
            }

            return graphics.VisibleClipBounds;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Printing;

/// <summary>
///  Specifies a print controller that sends information to a printer.
/// </summary>
public class StandardPrintController : PrintController
{
    /// <summary>
    ///  Implements StartPrint for printing to a physical printer.
    /// </summary>
    public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnStartPrint(document, e);
        throw new PlatformNotSupportedException("Physical printing requires a WinFormsX printing PAL implementation.");
    }

    /// <summary>
    ///  Implements StartPage for printing to a physical printer.
    /// </summary>
    public override Graphics OnStartPage(PrintDocument document, PrintPageEventArgs e)
    {
        throw new PlatformNotSupportedException("Physical printing requires a WinFormsX printing PAL implementation.");
    }

    /// <summary>
    ///  Implements EndPage for printing to a physical printer.
    /// </summary>
    public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
        base.OnEndPage(document, e);
    }

    /// <summary>
    ///  Implements EndPrint for printing to a physical printer.
    /// </summary>
    public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnEndPrint(document, e);
    }
}

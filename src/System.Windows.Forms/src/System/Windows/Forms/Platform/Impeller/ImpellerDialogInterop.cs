// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller dialog interop — common dialogs.
/// Backed by managed WinFormsX dialogs only; native OS dialog APIs are never used.
/// </summary>
internal sealed class ImpellerDialogInterop : IDialogInterop
{
    public string? ShowOpenFileDialog(string? title, string? filter, string? initialDir)
    {
        WarnUnsupported("OpenFileDialog");
        return null;
    }

    public string? ShowSaveFileDialog(string? title, string? filter, string? initialDir)
    {
        WarnUnsupported("SaveFileDialog");
        return null;
    }

    public bool ShowColorDialog(ref System.Drawing.Color color)
    {
        WarnUnsupported("ColorDialog");
        return false;
    }

    public bool ShowFontDialog(ref System.Drawing.Font font)
    {
        WarnUnsupported("FontDialog");
        return false;
    }

    public string? ShowFolderBrowserDialog(string? description)
    {
        WarnUnsupported("FolderBrowserDialog");
        return null;
    }

    private static void WarnUnsupported(string feature)
        => Console.Error.WriteLine($"[WINFORMSX_WARNING] {feature} is not implemented in the managed Impeller dialog layer yet; returning Cancel without native OS APIs.");
}

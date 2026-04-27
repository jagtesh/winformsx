// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller dialog interop — common dialogs.
/// Will be backed by platform-native dialogs (GTK/KDE on Linux, NSOpenPanel on macOS)
/// or custom Impeller-rendered dialogs.
/// </summary>
internal sealed class ImpellerDialogInterop : IDialogInterop
{
    public string? ShowOpenFileDialog(string? title, string? filter, string? initialDir) => null;
    public string? ShowSaveFileDialog(string? title, string? filter, string? initialDir) => null;
    public bool ShowColorDialog(ref System.Drawing.Color color) => false;
    public bool ShowFontDialog(ref System.Drawing.Font font) => false;
    public string? ShowFolderBrowserDialog(string? description) => null;
}

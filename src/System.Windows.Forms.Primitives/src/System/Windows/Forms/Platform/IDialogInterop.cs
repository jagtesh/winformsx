// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Common dialog abstraction — file open/save, color picker, font picker, folder browser.
/// Uses managed types instead of Win32 structs for cross-platform portability.
/// </summary>
internal interface IDialogInterop
{
    /// <summary>Show a file-open dialog. Returns the selected path, or null if cancelled.</summary>
    string? ShowOpenFileDialog(string? title, string? filter, string? initialDir);

    /// <summary>Show a file-save dialog. Returns the selected path, or null if cancelled.</summary>
    string? ShowSaveFileDialog(string? title, string? filter, string? initialDir);

    /// <summary>Show a color picker. Returns true if a color was selected.</summary>
    bool ShowColorDialog(ref System.Drawing.Color color);

    /// <summary>Show a font picker. Returns true if a font was selected.</summary>
    bool ShowFontDialog(ref System.Drawing.Font font);

    /// <summary>Show a folder browser. Returns the selected path, or null if cancelled.</summary>
    string? ShowFolderBrowserDialog(string? description);
}

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
    string[]? ShowOpenFileDialog(
        nint owner,
        string? title,
        string? filter,
        int filterIndex,
        string? initialDir,
        string? fileName,
        bool multiselect);

    /// <summary>Show a file-save dialog. Returns the selected path, or null if cancelled.</summary>
    string? ShowSaveFileDialog(nint owner, string? title, string? filter, int filterIndex, string? initialDir, string? fileName);

    /// <summary>Show a color picker. Returns true if a color was selected.</summary>
    bool ShowColorDialog(nint owner, ref System.Drawing.Color color, int[] customColors, bool allowFullOpen, bool fullOpen);

    /// <summary>Show a font picker. Returns true if a font was selected.</summary>
    bool ShowFontDialog(nint owner, ref System.Drawing.Font font, ref System.Drawing.Color color, bool showEffects, bool showColor);

    /// <summary>Show a folder browser. Returns the selected path, or null if cancelled.</summary>
    string? ShowFolderBrowserDialog(nint owner, string? description, string? initialDirectory, string? selectedPath);
}

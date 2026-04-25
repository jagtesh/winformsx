// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.UI.HiDpi;

internal readonly partial struct DPI_AWARENESS_CONTEXT
{
    internal static DPI_AWARENESS_CONTEXT UNSPECIFIED_DPI_AWARENESS_CONTEXT { get; } = (DPI_AWARENESS_CONTEXT)0;

    /// <summary>
    ///  Compares <see cref="DPI_AWARENESS"/> for equality.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the specified context is equal; otherwise, <see langword="false"/> if not equal
    ///  or the underlying OS does not support comparing context.
    /// </returns>
    public bool IsEquivalent(DPI_AWARENESS_CONTEXT dpiContext)
    {
        if (this == UNSPECIFIED_DPI_AWARENESS_CONTEXT && dpiContext == UNSPECIFIED_DPI_AWARENESS_CONTEXT)
        {
            return true;
        }

        // Direct value comparison — works across PAL/Impeller without requiring
        // the Win32 AreDpiAwarenessContextsEqual API.
        return Value == dpiContext.Value;
    }
}

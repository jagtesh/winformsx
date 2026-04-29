// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL TranslateMessage(in MSG lpMsg)
        => PlatformApi.Message.TranslateMessage(in lpMsg);

    /// <inheritdoc cref="TranslateMessage(in MSG)"/>
    public static unsafe BOOL TranslateMessage(MSG* lpMsg)
        => lpMsg is null ? false : TranslateMessage(in *lpMsg);

    /// <inheritdoc cref="TranslateMessage(in MSG)"/>
    public static BOOL TranslateMessage(MSG lpMsg)
        => TranslateMessage(in lpMsg);
}

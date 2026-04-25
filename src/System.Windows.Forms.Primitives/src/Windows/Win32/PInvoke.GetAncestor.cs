// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static HWND GetAncestor(HWND hwnd, GET_ANCESTOR_FLAGS gaFlags)
        => PlatformApi.Window.GetAncestor(hwnd, gaFlags);

    /// <inheritdoc cref="GetAncestor(HWND, GET_ANCESTOR_FLAGS)"/>
    public static HWND GetAncestor<T>(T hwnd, GET_ANCESTOR_FLAGS gaFlags) where T : IHandle<HWND>
    {
        HWND result = GetAncestor(hwnd.Handle, gaFlags);
        GC.KeepAlive(hwnd.Wrapper);
        return result;
    }
}
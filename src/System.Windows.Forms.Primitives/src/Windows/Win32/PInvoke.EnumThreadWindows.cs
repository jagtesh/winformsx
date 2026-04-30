// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public delegate BOOL EnumThreadWindowsCallback(HWND hWnd);

    /// <summary>
    ///  Enumerates all nonchild windows in the current thread.
    /// </summary>
    public static unsafe BOOL EnumCurrentThreadWindows(EnumThreadWindowsCallback callback)
    {
        return PlatformApi.Window.EnumWindows(hwnd => callback(hwnd));
    }
}

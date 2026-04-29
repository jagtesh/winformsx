// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public delegate BOOL EnumChildWindowsCallback(HWND hWnd);

    public static unsafe BOOL EnumChildWindows<T>(T hwndParent, EnumChildWindowsCallback callback)
        where T : IHandle<HWND>
    {
        BOOL result = PlatformApi.Window.EnumChildWindows(hwndParent.Handle, child => callback(child));
        GC.KeepAlive(hwndParent.Wrapper);
        return result;
    }
}

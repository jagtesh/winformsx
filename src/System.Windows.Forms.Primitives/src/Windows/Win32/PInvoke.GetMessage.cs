// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL GetMessage(MSG* lpMsg, HWND hWnd, uint wMsgFilterMin, uint wMsgFilterMax)
    {
        BOOL result = PlatformApi.Message.GetMessage(out MSG msg, hWnd, wMsgFilterMin, wMsgFilterMax);
        if (lpMsg is not null)
        {
            *lpMsg = msg;
        }

        return result;
    }

    /// <inheritdoc cref="GetMessage(MSG*, HWND, uint, uint)"/>
    public static unsafe BOOL GetMessage<T>(MSG* lpMsg, T hWnd, uint wMsgFilterMin, uint wMsgFilterMax)
        where T : IHandle<HWND>
    {
        BOOL result = GetMessage(lpMsg, hWnd.Handle, wMsgFilterMin, wMsgFilterMax);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}

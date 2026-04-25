// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL PostMessage(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
        => PlatformApi.Message.PostMessage(hWnd, Msg, wParam, lParam);

    /// <inheritdoc cref="PostMessage(HWND, uint, WPARAM, LPARAM)"/>
    public static BOOL PostMessage<T>(T hWnd, uint Msg, WPARAM wParam = default, LPARAM lParam = default) where T : IHandle<HWND>
    {
        BOOL result = PostMessage(hWnd.Handle, Msg, wParam, lParam);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}
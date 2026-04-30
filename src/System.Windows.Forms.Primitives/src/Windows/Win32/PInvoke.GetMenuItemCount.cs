// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static int GetMenuItemCount(HMENU hMenu)
        => PlatformApi.Control.GetMenuItemCount(hMenu);

    /// <inheritdoc cref="GetMenuItemCount(HMENU)"/>
    public static int GetMenuItemCount<T>(T hMenu)
        where T : IHandle<HMENU>
    {
        int result = GetMenuItemCount(hMenu.Handle);
        GC.KeepAlive(hMenu.Wrapper);
        return result;
    }
}

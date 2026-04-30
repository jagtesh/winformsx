// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL GetMenuItemInfo(HMENU hmenu, uint item, BOOL fByPosition, ref MENUITEMINFOW lpmii)
        => PlatformApi.Control.GetMenuItemInfo(hmenu, item, fByPosition, ref lpmii);

    public static BOOL GetMenuItemInfo<T>(T hmenu, uint item, BOOL fByPosition, ref MENUITEMINFOW lpmii)
        where T : IHandle<HMENU>
    {
        BOOL result = GetMenuItemInfo(hmenu.Handle, item, fByPosition, ref lpmii);
        GC.KeepAlive(hmenu.Wrapper);
        return result;
    }
}

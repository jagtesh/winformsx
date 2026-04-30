// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.UI.WindowsAndMessaging;

internal struct MENUITEMINFOW
{
    public uint cbSize;
    public MENU_ITEM_MASK fMask;
    public MENU_ITEM_TYPE fType;
    public MENU_ITEM_STATE fState;
    public uint wID;
    public HMENU hSubMenu;
    public HBITMAP hbmpChecked;
    public HBITMAP hbmpUnchecked;
    public nuint dwItemData;
    public PWSTR dwTypeData;
    public uint cch;
    public HBITMAP hbmpItem;
}

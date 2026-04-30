// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.UI.WindowsAndMessaging;

[Flags]
internal enum MENU_ITEM_STATE : uint
{
    MFS_ENABLED = 0x00000000,
    MFS_UNCHECKED = 0x00000000,
    MFS_UNHILITE = 0x00000000,
    MFS_GRAYED = 0x00000003,
    MFS_DISABLED = MFS_GRAYED,
    MFS_CHECKED = 0x00000008,
    MFS_HILITE = 0x00000080,
    MFS_DEFAULT = 0x00001000,
}

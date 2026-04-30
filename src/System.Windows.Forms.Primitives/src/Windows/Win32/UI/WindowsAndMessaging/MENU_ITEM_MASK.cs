// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.UI.WindowsAndMessaging;

[Flags]
internal enum MENU_ITEM_MASK : uint
{
    MIIM_STATE = 0x00000001,
    MIIM_ID = 0x00000002,
    MIIM_SUBMENU = 0x00000004,
    MIIM_CHECKMARKS = 0x00000008,
    MIIM_TYPE = 0x00000010,
    MIIM_DATA = 0x00000020,
    MIIM_STRING = 0x00000040,
    MIIM_BITMAP = 0x00000080,
    MIIM_FTYPE = 0x00000100,
}

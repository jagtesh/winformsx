// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.UI.WindowsAndMessaging;

[Flags]
internal enum MENU_ITEM_TYPE : uint
{
    MFT_STRING = 0x00000000,
    MFT_BITMAP = 0x00000004,
    MFT_MENUBARBREAK = 0x00000020,
    MFT_MENUBREAK = 0x00000040,
    MFT_OWNERDRAW = 0x00000100,
    MFT_RADIOCHECK = 0x00000200,
    MFT_SEPARATOR = 0x00000800,
    MFT_RIGHTORDER = 0x00002000,
    MFT_RIGHTJUSTIFY = 0x00004000,
}

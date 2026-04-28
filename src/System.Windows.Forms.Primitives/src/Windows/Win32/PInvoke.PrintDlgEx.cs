// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

internal static partial class PInvoke
{
    internal static unsafe HRESULT PrintDlgEx(PRINTDLGEXW* pPD)
    {
        _ = pPD;
        return HRESULT.COR_E_NOTSUPPORTED;
    }
}

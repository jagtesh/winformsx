// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

// https://github.com/microsoft/win32metadata/issues/1300
internal static partial class PInvoke
{
    public static unsafe BOOL GetOpenFileName(OPENFILENAME* param0)
    {
        _ = param0;
        return BOOL.FALSE;
    }

    public static unsafe BOOL GetSaveFileName(OPENFILENAME* param0)
    {
        _ = param0;
        return BOOL.FALSE;
    }
}

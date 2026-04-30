// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls;

namespace System.Windows.Forms.Primitives.Tests.Interop.ComCtl32;

public class CommonControlsCompatibilityTests
{
    [Fact]
    public void InitCommonControlsEx_ValidSize_RecordsRequestedClasses()
    {
        INITCOMMONCONTROLSEX_ICC flags =
            INITCOMMONCONTROLSEX_ICC.ICC_LISTVIEW_CLASSES
            | INITCOMMONCONTROLSEX_ICC.ICC_TREEVIEW_CLASSES
            | INITCOMMONCONTROLSEX_ICC.ICC_DATE_CLASSES;

        INITCOMMONCONTROLSEX icc = new()
        {
            dwSize = (uint)global::System.Runtime.InteropServices.Marshal.SizeOf<INITCOMMONCONTROLSEX>(),
            dwICC = flags
        };

        Assert.True(PInvoke.InitCommonControlsEx(icc));
        Assert.True(PInvoke.AreCommonControlClassesInitialized(flags));
    }

    [Fact]
    public void InitCommonControls_RecordsWin95Classes()
    {
        PInvoke.InitCommonControls();

        Assert.True(PInvoke.AreCommonControlClassesInitialized((INITCOMMONCONTROLSEX_ICC)0x000000FF));
    }

    [Fact]
    public void InitCommonControlsEx_InvalidSize_ReturnsFalse()
    {
        INITCOMMONCONTROLSEX icc = new()
        {
            dwSize = 0,
            dwICC = INITCOMMONCONTROLSEX_ICC.ICC_TAB_CLASSES
        };

        Assert.False(PInvoke.InitCommonControlsEx(icc));
    }
}

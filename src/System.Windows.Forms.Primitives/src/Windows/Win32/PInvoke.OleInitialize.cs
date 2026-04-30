// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe HRESULT OleInitialize(void* pvReserved)
    {
        _ = pvReserved;

        WinFormsXCompatibilityWarning.Once(
            "PInvoke.OleInitialize",
            "OLE apartment initialization is routed through WinFormsX managed compatibility mode.");

        return HRESULT.S_OK;
    }
}

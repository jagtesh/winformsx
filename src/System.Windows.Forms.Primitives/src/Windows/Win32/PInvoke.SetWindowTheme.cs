// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>No-op via PAL — visual themes not supported in Impeller.</summary>
    public static HRESULT SetWindowTheme(HWND hwnd, string pszSubAppName, string? pszSubIdList)
        => HRESULT.S_OK;

    /// <summary>PCWSTR overload.</summary>
    public static unsafe HRESULT SetWindowTheme(HWND hwnd, PCWSTR pszSubAppName, PCWSTR pszSubIdList)
        => HRESULT.S_OK;
}

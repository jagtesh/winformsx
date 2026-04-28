// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>No-op via PAL — visual themes not supported in Impeller.</summary>
    public static HRESULT SetWindowTheme(HWND hwnd, string pszSubAppName, string? pszSubIdList)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.SetWindowTheme",
            "Native uxtheme styling is ignored in WinFormsX; visual state must be rendered by the managed PAL.");
        return HRESULT.S_OK;
    }

    /// <summary>PCWSTR overload.</summary>
    public static unsafe HRESULT SetWindowTheme(HWND hwnd, PCWSTR pszSubAppName, PCWSTR pszSubIdList)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.SetWindowTheme",
            "Native uxtheme styling is ignored in WinFormsX; visual state must be rendered by the managed PAL.");
        return HRESULT.S_OK;
    }
}

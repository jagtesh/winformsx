// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static HRSRC FindResource(HMODULE hModule, PCWSTR lpName, PCWSTR lpType)
        => PlatformApi.System.FindResource(hModule, lpName, lpType);

    public static HRSRC FindResourceEx(HMODULE hModule, PCWSTR lpType, PCWSTR lpName, ushort wLanguage)
        => PlatformApi.System.FindResourceEx(hModule, lpType, lpName, wLanguage);

    public static HGLOBAL LoadResource(HMODULE hModule, HRSRC hResInfo)
        => PlatformApi.System.LoadResource(hModule, hResInfo);

    public static unsafe void* LockResource(HGLOBAL hResData)
        => PlatformApi.System.LockResource(hResData);

    public static uint SizeofResource(HMODULE hModule, HRSRC hResInfo)
        => PlatformApi.System.SizeofResource(hModule, hResInfo);

    public static BOOL FreeResource(HGLOBAL hResData)
        => PlatformApi.System.FreeResource(hResData);
}

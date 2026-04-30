// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.Threading;

namespace Windows.Win32;

internal static unsafe partial class PInvoke
{
    public static BOOL CloseHandle(HANDLE hObject)
        => PlatformApi.System.CloseHandle(hObject);

    public static BOOL DuplicateHandle(
        HANDLE hSourceProcessHandle,
        HANDLE hSourceHandle,
        HANDLE hTargetProcessHandle,
        HANDLE* lpTargetHandle,
        uint dwDesiredAccess,
        BOOL bInheritHandle,
        uint dwOptions)
        => PlatformApi.System.DuplicateHandle(
            hSourceProcessHandle,
            hSourceHandle,
            hTargetProcessHandle,
            lpTargetHandle,
            dwDesiredAccess,
            bInheritHandle,
            dwOptions);

    public static uint FormatMessage(
        FORMAT_MESSAGE_OPTIONS dwFlags,
        void* lpSource,
        uint dwMessageId,
        uint dwLanguageId,
        char* lpBuffer,
        uint nSize,
        void* Arguments)
        => PlatformApi.System.FormatMessage(dwFlags, lpSource, dwMessageId, dwLanguageId, lpBuffer, nSize, Arguments);

    public static BOOL GetExitCodeThread(HANDLE hThread, uint* lpExitCode)
        => PlatformApi.System.GetExitCodeThread(hThread, lpExitCode);

    public static int GetLocaleInfoEx(string lpLocaleName, uint LCType, char* lpLCData, int cchData)
        => PlatformApi.System.GetLocaleInfoEx(lpLocaleName, LCType, lpLCData, cchData);

    public static void GetStartupInfo(out STARTUPINFOW lpStartupInfo)
        => PlatformApi.System.GetStartupInfo(out lpStartupInfo);

    public static uint GetThreadLocale()
        => PlatformApi.System.GetThreadLocale();

    public static uint GetTickCount()
        => PlatformApi.System.GetTickCount();
}

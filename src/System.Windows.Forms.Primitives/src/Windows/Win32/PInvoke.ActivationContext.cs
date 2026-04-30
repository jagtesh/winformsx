// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;
using Windows.Win32.System.ApplicationInstallationAndServicing;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public const uint ACTCTX_FLAG_RESOURCE_NAME_VALID = 0x00000008;
    public const uint ACTCTX_FLAG_HMODULE_VALID = 0x00000080;

    public static unsafe HANDLE CreateActCtx(ACTCTXW* pActCtx)
        => PlatformApi.System.CreateActCtx(pActCtx);

    public static unsafe bool ActivateActCtx(HANDLE hActCtx, nuint* lpCookie)
        => PlatformApi.System.ActivateActCtx(hActCtx, lpCookie);

    public static bool DeactivateActCtx(uint dwFlags, nuint ulCookie)
        => PlatformApi.System.DeactivateActCtx(dwFlags, ulCookie);

    public static unsafe bool GetCurrentActCtx(HANDLE* lphActCtx)
        => PlatformApi.System.GetCurrentActCtx(lphActCtx);
}

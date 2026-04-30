// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe uint GetModuleFileName(HINSTANCE hModule, char* lpFilename, uint nSize)
    {
        if (lpFilename is null || nSize == 0)
        {
            return 0;
        }

        int length = nSize > int.MaxValue ? int.MaxValue : (int)nSize;
        return PlatformApi.System.GetModuleFileName((HMODULE)(nint)hModule, new Span<char>(lpFilename, length));
    }
}

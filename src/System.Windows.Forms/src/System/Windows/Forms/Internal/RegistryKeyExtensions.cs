// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;

namespace System.Windows.Forms;

internal static class RegistryKeyExtensions
{
    public static string? GetMUIString(this RegistryKey? key, string keyName, string fallbackKeyName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return key?.GetValue(fallbackKeyName) as string;
        }

        return key is not null
            ? PInvoke.RegLoadMUIString(key, keyName, out string localizedValue)
                ? localizedValue
                : key.GetValue(fallbackKeyName) is string value
                    ? value
                    : null
            : null;
    }
}

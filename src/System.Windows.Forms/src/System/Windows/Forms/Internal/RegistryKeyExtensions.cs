// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;

namespace System.Windows.Forms;

internal static class RegistryKeyExtensions
{
    public static string? GetMUIString(this RegistryKey? key, string keyName, string fallbackKeyName)
    {
        if (key is null)
        {
            return null;
        }

        try
        {
            if (PInvoke.RegLoadMUIString(key, keyName, out string localizedValue))
            {
                return localizedValue;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        {
        }

        return key.GetValue(fallbackKeyName) is string value ? value : null;
    }
}

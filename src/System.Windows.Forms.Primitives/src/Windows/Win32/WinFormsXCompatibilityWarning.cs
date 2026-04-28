// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static class WinFormsXCompatibilityWarning
{
    private static readonly HashSet<string> s_seen = [];
    private static readonly object s_lock = new();

    public static void Once(string key, string message)
    {
        lock (s_lock)
        {
            if (!s_seen.Add(key))
            {
                return;
            }
        }

        Console.Error.WriteLine($"[WINFORMSX_WARNING] {message}");
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    private static readonly object s_commonControlsLock = new();
    private static INITCOMMONCONTROLSEX_ICC s_initializedCommonControlClasses;

    internal static INITCOMMONCONTROLSEX_ICC InitializedCommonControlClasses
    {
        get
        {
            lock (s_commonControlsLock)
            {
                return s_initializedCommonControlClasses;
            }
        }
    }

    internal static bool AreCommonControlClassesInitialized(INITCOMMONCONTROLSEX_ICC flags) =>
        (InitializedCommonControlClasses & flags) == flags;

    internal static void RegisterCommonControlClasses(INITCOMMONCONTROLSEX_ICC flags)
    {
        lock (s_commonControlsLock)
        {
            s_initializedCommonControlClasses |= flags;
        }
    }

    /// <summary>No-op via PAL — no comctl32 in Impeller.</summary>
    public static BOOL InitCommonControlsEx(in INITCOMMONCONTROLSEX picce)
    {
        if (picce.dwSize != (uint)global::System.Runtime.InteropServices.Marshal.SizeOf<INITCOMMONCONTROLSEX>())
        {
            return false;
        }

        RegisterCommonControlClasses(picce.dwICC);
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.InitCommonControlsEx",
            "Native comctl32 common controls are not loaded in WinFormsX; InitCommonControlsEx acknowledged without native work.");
        return true;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.GdiPlus;

/// <summary>
///  Helper to ensure GDI+ is initialized before making calls.
/// </summary>
internal static partial class GdiPlusInitialization
{
    /// <summary>
    ///  Returns true if GDI+ has been started.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This should be called anywhere you make <see cref="PInvokeCore"/> calls to GDI+ where you don't
    ///   already have a GDI+ handle. In System.Drawing.Common, this is done in the PInvoke static constructor
    ///   so it is not necessary for methods defined there.
    ///  </para>
    ///  <para>
    ///   We don't do this implicitly in the Core assembly to avoid unnecessary loading of GDI+.
    ///  </para>
    ///  <para>
    ///   https://github.com/microsoft/CsWin32/issues/1308 tracks a proposal to make this more automatic.
    ///  </para>
    /// </remarks>
    internal static bool EnsureInitialized()
    {
        // WinFormsX is Impeller-only on every host OS, including Windows.
        // Never bind gdiplus.dll; callers must route drawing through PAL.
        return false;
    }
}

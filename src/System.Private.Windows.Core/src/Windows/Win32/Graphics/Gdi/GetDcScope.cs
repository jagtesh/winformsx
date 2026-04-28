// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.Gdi;

/// <summary>
///  Helper to scope lifetime of a PAL-managed <see cref="Gdi.HDC"/>. Releases the <see cref="Gdi.HDC"/> (if any)
///  through the PAL callback when disposed.
/// </summary>
/// <remarks>
///  <para>
///   Use in a <see langword="using" /> statement. If you must pass this around, always pass by <see langword="ref" />
///   to avoid duplicating the handle and risking a double release.
///  </para>
/// </remarks>
internal readonly ref struct GetDcScope
{
    public static Func<HWND, HDC>? GetDCCallback { get; set; }
    public static Func<HWND, HRGN, GET_DCX_FLAGS, HDC>? GetDCExCallback { get; set; }
    public static Action<HWND, HDC>? ReleaseDCCallback { get; set; }

    public HDC HDC { get; }
    public HWND HWND { get; }

    public GetDcScope(HWND hwnd)
    {
        HWND = hwnd;
        HDC = GetDCCallback is object ? GetDCCallback(hwnd) : CreateSyntheticHdc();
    }

    /// <summary>
    ///  Creates a PAL-managed <see cref="Gdi.HDC"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   GetWindowDC calls GetDCEx(hwnd, null, DCX_WINDOW | DCX_USESTYLE).
    ///  </para>
    ///  <para>
    ///   GetDC calls GetDCEx(hwnd, null, DCX_USESTYLE) when given a handle. (When given null it has additional
    ///   logic, and can't be replaced directly by GetDCEx.
    ///  </para>
    /// </remarks>
    public GetDcScope(HWND hwnd, HRGN hrgnClip, GET_DCX_FLAGS flags)
    {
        HWND = hwnd;
        HDC = GetDCExCallback is object ? GetDCExCallback(hwnd, hrgnClip, flags) : CreateSyntheticHdc();
    }

    /// <summary>
    ///  Creates a DC scope for the primary monitor (not the entire desktop).
    /// </summary>
    /// <remarks>
    ///   <para>
    ///    The screen DC is a synthetic compatibility handle in WinFormsX.
    ///   </para>
    /// </remarks>
    public static GetDcScope ScreenDC => new(HWND.Null);

    public bool IsNull => HDC.IsNull;

    public static implicit operator nint(in GetDcScope scope) => scope.HDC;
    public static implicit operator HDC(in GetDcScope scope) => scope.HDC;

    private static HDC CreateSyntheticHdc() => (HDC)(nint)1;

    public void Dispose()
    {
        if (!HDC.IsNull)
        {
            if (ReleaseDCCallback is object)
            {
                ReleaseDCCallback(HWND, HDC);
            }
            else
            {
                // PAL-managed synthetic HDCs have no native lifetime.
            }
        }
    }
}

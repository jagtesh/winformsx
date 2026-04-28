// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.Gdi;

/// <summary>
///  Helper to scope lifetime of a PAL-managed <see cref="Gdi.HBITMAP"/> placeholder.
/// </summary>
/// <remarks>
///  <para>
///   Use in a <see langword="using" /> statement. If you must pass this around, always pass
///   by <see langword="ref" /> to avoid duplicating the handle and risking a double delete.
///  </para>
/// </remarks>
#if DEBUG
internal class CreateBitmapScope : DisposalTracking.Tracker, IDisposable
#else
internal readonly ref struct CreateBitmapScope
#endif
{
    public HBITMAP HBITMAP { get; }

    /// <summary>
    ///  Creates a PAL-managed bitmap placeholder.
    /// </summary>
    public unsafe CreateBitmapScope(int nWidth, int nHeight, uint nPlanes, uint nBitCount, void* lpvBits) =>
        HBITMAP = CreateSyntheticBitmap();

    /// <summary>
    ///  Creates a PAL-managed bitmap placeholder compatible with the given <see cref="HDC"/>.
    /// </summary>
    public CreateBitmapScope(HDC hdc, int cx, int cy) => HBITMAP = CreateSyntheticBitmap();

    public static implicit operator HBITMAP(in CreateBitmapScope scope) => scope.HBITMAP;
    public static implicit operator HGDIOBJ(in CreateBitmapScope scope) => scope.HBITMAP;
    public static explicit operator nint(in CreateBitmapScope scope) => scope.HBITMAP;

    public bool IsNull => HBITMAP.IsNull;

    private static HBITMAP CreateSyntheticBitmap() => (HBITMAP)(nint)1;

    public void Dispose()
    {
#if DEBUG
        GC.SuppressFinalize(this);
#endif
    }
}

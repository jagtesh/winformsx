// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using Windows.Win32.Graphics.GdiPlus;

namespace Windows.Win32.Graphics.Gdi;

/// <summary>
///  Helper to scope PAL-managed GDI-shaped regions.
/// </summary>
/// <remarks>
///  <para>
///   Use in a <see langword="using" /> statement. If you must pass this around, always pass
///   by <see langword="ref" /> to avoid duplicating the handle and risking a double deletion.
///  </para>
/// </remarks>
#if DEBUG
internal unsafe class RegionScope : DisposalTracking.Tracker, IDisposable
#else
internal unsafe ref struct RegionScope
#endif
{
    public HRGN Region { get; private set; }

    /// <summary>
    ///  Creates a PAL-managed region placeholder for the given rectangle.
    /// </summary>
    public RegionScope(Rectangle rectangle) =>
        Region = CreateSyntheticRegion();

    /// <summary>
    ///  Creates a PAL-managed region placeholder for the given rectangle.
    /// </summary>
    public RegionScope(int x1, int y1, int x2, int y2) =>
        Region = CreateSyntheticRegion();

    /// <summary>
    ///  Creates a clipping region copy for the given PAL-managed device context.
    /// </summary>
    /// <param name="hdc">Handle to a device context to copy the clipping region from.</param>
    public RegionScope(HDC hdc) => Region = default;

    /// <summary>
    ///  Creates a native region from a GDI+ <see cref="GpRegion"/>.
    /// </summary>
    public RegionScope(IPointer<GpRegion> region, IPointer<GpGraphics> graphics)
    {
        throw new NotSupportedException("Native GDI+ regions are not supported by the managed drawing PAL.");
    }

    public RegionScope(IPointer<GpRegion> region, HWND hwnd)
    {
        throw new NotSupportedException("Native GDI+ regions are not supported by the managed drawing PAL.");
    }

    /// <summary>
    ///  Returns true if this represents a null HRGN.
    /// </summary>
#if DEBUG
    public bool IsNull => Region.IsNull;
#else
    public readonly bool IsNull => Region.IsNull;
#endif

    public static implicit operator HRGN(RegionScope regionScope) => regionScope.Region;

    /// <summary>
    ///  Clears the handle. Use this to hand over ownership to another entity.
    /// </summary>
    public void RelinquishOwnership() => Region = default;

    private static HRGN CreateSyntheticRegion() => (HRGN)(nint)1;

#if DEBUG
    public void Dispose()
#else
    public readonly void Dispose()
#endif
    {
#if DEBUG
        GC.SuppressFinalize(this);
#endif
    }
}

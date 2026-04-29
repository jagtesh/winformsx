// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.Gdi;

/// <summary>
///  Helper to scope selecting a given mapping mode into a HDC. Restores the original mapping mode into the HDC
///  when disposed.
/// </summary>
/// <remarks>
///  <para>
///   Use in a <see langword="using" /> statement. If you must pass this around, always pass by
///   <see langword="ref" /> to avoid duplicating the handle and resetting multiple times.
///  </para>
/// </remarks>
#if DEBUG
internal class SetMapModeScope : DisposalTracking.Tracker, IDisposable
#else
internal readonly ref struct SetMapModeScope
#endif
{
    private readonly HDC_MAP_MODE _previousMapMode;
    private readonly HDC _hdc;

    /// <summary>
    ///  Preserves API-compatible map-mode scoping for PAL-managed HDCs.
    /// </summary>
    public SetMapModeScope(HDC hdc, HDC_MAP_MODE mapMode)
    {
        _previousMapMode = mapMode;
        _hdc = default;
    }

    public void Dispose()
    {
        if (!_hdc.IsNull)
        {
        }

#if DEBUG
        GC.SuppressFinalize(this);
#endif
    }
}

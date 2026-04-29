// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Windows.Forms.Platform;

namespace Windows.Win32.Graphics.Gdi;

/// <summary>
///  Helper to scope the lifetime of a <see cref="Gdi.HPEN"/>.
/// </summary>
/// <remarks>
///  <para>
///   Use in a <see langword="using" /> statement. If you must pass this around, always pass
///   by <see langword="ref" /> to avoid duplicating the handle and risking a double delete.
///  </para>
/// </remarks>
#if DEBUG
internal class CreatePenScope : DisposalTracking.Tracker, IDisposable
#else
internal readonly ref struct CreatePenScope
#endif
{
    public HPEN HPEN { get; }

    /// <summary>
    ///  Creates a solid pen based on the <paramref name="color"/> and <paramref name="width"/> using the PAL.
    /// </summary>
    public CreatePenScope(Color color, int width = 1) =>
        HPEN = PlatformApi.Gdi.CreatePen(PEN_STYLE.PS_SOLID, width, (COLORREF)(uint)ColorTranslator.ToWin32(color));

    public static implicit operator HPEN(in CreatePenScope scope) => scope.HPEN;
    public static implicit operator HGDIOBJ(in CreatePenScope scope) => (HGDIOBJ)scope.HPEN.Value;

    public bool IsNull => HPEN.IsNull;

    public void Dispose()
    {
        if (!HPEN.IsNull)
        {
            PlatformApi.Gdi.DeleteObject(HPEN);
        }

#if DEBUG
        GC.SuppressFinalize(this);
#endif
    }
}

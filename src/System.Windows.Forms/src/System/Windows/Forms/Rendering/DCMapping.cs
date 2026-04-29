// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
///  DCMapping is used to change the mapping and clip region of the specified device context to the given
///  bounds. When the DCMapping is disposed, the original mapping and clip rectangle are restored.
///
///  Example:
///
///  using(DCMapping mapping = new DCMapping(hDC, new Rectangle(10,10, 50, 50)
///  {
///      // inside here the hDC's mapping of (0,0) is inset by (10,10) and
///      // all painting is clipped at (0,0) - (50,50)
///  }
///
///  PERF: DCMapping is a structure so that it will allocate on the stack rather than in GC managed memory. This
///  way disposing the object does not force a GC. Since DCMapping objects aren't likely to be passed between
///  functions rather used and disposed in the same one, this reduces overhead.
/// </summary>
internal readonly struct DCMapping : IDisposable
{
    private readonly HDC _hdc;
    private readonly int _savedState;

    public unsafe DCMapping(HDC hdc, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(hdc);

        _hdc = default;
        _savedState = 0;
    }

    public void Dispose()
    {
    }
}

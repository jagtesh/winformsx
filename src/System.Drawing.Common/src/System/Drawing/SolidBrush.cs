// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Internal;

namespace System.Drawing;

public sealed unsafe class SolidBrush : Brush, ISystemColorTracker
{
    // GDI+ doesn't understand system colors, so we need to cache the value here.
    private Color _color = Color.Empty;
    private bool _immutable;

    public SolidBrush(Color color)
    {
        _color = color;

        if (_color.IsSystemColor)
        {
            SystemColorTracker.Add(this);
        }
    }

    internal SolidBrush(Color color, bool immutable) : this(color) => _immutable = immutable;

    internal SolidBrush(GpSolidFill* nativeBrush)
    {
        Debug.Assert(nativeBrush is not null, "Initializing native brush with null.");
        SetNativeBrushInternal((GpBrush*)nativeBrush);
    }

    public override object Clone()
    {
        // Clones of immutable brushes are not immutable.
        return new SolidBrush(_color);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            _immutable = false;
        }
        else if (_immutable)
        {
            throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, "Brush"));
        }

        base.Dispose(disposing);
    }

    public Color Color
    {
        get
        {
            return _color;
        }
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, "Brush"));
            }

            if (_color != value)
            {
                Color oldColor = _color;
                InternalSetColor(value);

                // NOTE: We never remove brushes from the active list, so if someone is
                // changing their brush colors a lot, this could be a problem.
                if (value.IsSystemColor && !oldColor.IsSystemColor)
                {
                    SystemColorTracker.Add(this);
                }
            }
        }
    }

    // Sets the color even if the brush is considered immutable.
    private void InternalSetColor(Color value)
    {
        _color = value;
    }

    void ISystemColorTracker.OnSystemColorChanged()
    {
        InternalSetColor(_color);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Drawing2D;

public sealed unsafe partial class AdjustableArrowCap : CustomLineCap
{
    private float _height;
    private float _width;
    private float _middleInset;
    private bool _filled;

    public AdjustableArrowCap(float width, float height) : this(width, height, true) { }

    public AdjustableArrowCap(float width, float height, bool isFilled)
    {
        _width = width;
        _height = height;
        _filled = isFilled;
        BaseCap = LineCap.Triangle;
    }

    public float Height
    {
        get
        {
            ThrowIfDisposed();
            return _height;
        }
        set
        {
            ThrowIfDisposed();
            _height = value;
        }
    }

    public float Width
    {
        get
        {
            ThrowIfDisposed();
            return _width;
        }
        set
        {
            ThrowIfDisposed();
            _width = value;
        }
    }

    public float MiddleInset
    {
        get
        {
            ThrowIfDisposed();
            return _middleInset;
        }
        set
        {
            ThrowIfDisposed();
            _middleInset = value;
        }
    }

    public bool Filled
    {
        get
        {
            ThrowIfDisposed();
            return _filled;
        }
        set
        {
            ThrowIfDisposed();
            _filled = value;
        }
    }

    internal override object CoreClone()
    {
        ThrowIfDisposed();
        AdjustableArrowCap clone = new(_width, _height, _filled)
        {
            MiddleInset = _middleInset,
            BaseCap = BaseCap,
            BaseInset = BaseInset,
            StrokeJoin = StrokeJoin,
            WidthScale = WidthScale
        };
        GetStrokeCaps(out LineCap startCap, out LineCap endCap);
        clone.SetStrokeCaps(startCap, endCap);
        return clone;
    }
}

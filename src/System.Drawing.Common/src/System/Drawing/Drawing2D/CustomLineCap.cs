// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace System.Drawing.Drawing2D;

public unsafe class CustomLineCap : MarshalByRefObject, ICloneable, IDisposable
{
    private GraphicsPath? _fillPath;
    private GraphicsPath? _strokePath;
    private LineCap _startCap = LineCap.Flat;
    private LineCap _endCap = LineCap.Flat;
    private LineJoin _strokeJoin = LineJoin.Miter;
    private LineCap _baseCap = LineCap.Flat;
    private float _baseInset;
    private float _widthScale = 1f;
    private bool _disposed;

    // For subclass creation
    internal CustomLineCap() { }

    public CustomLineCap(GraphicsPath? fillPath, GraphicsPath? strokePath) : this(fillPath, strokePath, LineCap.Flat) { }

    public CustomLineCap(GraphicsPath? fillPath, GraphicsPath? strokePath, LineCap baseCap) : this(fillPath, strokePath, baseCap, 0) { }

    public CustomLineCap(GraphicsPath? fillPath, GraphicsPath? strokePath, LineCap baseCap, float baseInset)
    {
        ValidateFillPath(fillPath);
        _fillPath = (GraphicsPath?)fillPath?.Clone();
        _strokePath = (GraphicsPath?)strokePath?.Clone();
        _baseCap = NormalizeCtorBaseCap(baseCap);
        _baseInset = baseInset;
    }

    internal static CustomLineCap CreateCustomLineCapObject(GpCustomLineCap* cap)
    {
        throw new PlatformNotSupportedException("WinFormsX custom line caps are managed and cannot wrap native GDI+ line-cap handles.");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _fillPath?.Dispose();
        _strokePath?.Dispose();
        _fillPath = null;
        _strokePath = null;
        _disposed = true;
    }

    ~CustomLineCap() => Dispose(false);

    public object Clone() => CoreClone();

    internal virtual object CoreClone()
    {
        ThrowIfDisposed();
        CustomLineCap clone = new(_fillPath, _strokePath, _baseCap, _baseInset);
        clone.SetStrokeCaps(_startCap, _endCap);
        clone.StrokeJoin = _strokeJoin;
        clone.WidthScale = _widthScale;
        return clone;
    }

    public void SetStrokeCaps(LineCap startCap, LineCap endCap)
    {
        ThrowIfDisposed();
        ValidateStrokeCap(startCap);
        ValidateStrokeCap(endCap);
        _startCap = startCap;
        _endCap = endCap;
    }

    public void GetStrokeCaps(out LineCap startCap, out LineCap endCap)
    {
        ThrowIfDisposed();
        startCap = _startCap;
        endCap = _endCap;
    }

    public LineJoin StrokeJoin
    {
        get
        {
            ThrowIfDisposed();
            return _strokeJoin;
        }
        set
        {
            ThrowIfDisposed();
            _strokeJoin = value;
        }
    }

    public LineCap BaseCap
    {
        get
        {
            ThrowIfDisposed();
            return _baseCap;
        }
        set
        {
            ThrowIfDisposed();
            ValidateBaseCap(value);
            _baseCap = value;
        }
    }

    public float BaseInset
    {
        get
        {
            ThrowIfDisposed();
            return _baseInset;
        }
        set
        {
            ThrowIfDisposed();
            _baseInset = value;
        }
    }

    public float WidthScale
    {
        get
        {
            ThrowIfDisposed();
            return _widthScale;
        }
        set
        {
            ThrowIfDisposed();
            _widthScale = value;
        }
    }

    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static LineCap NormalizeCtorBaseCap(LineCap baseCap)
        => IsBasicLineCap(baseCap) ? baseCap : LineCap.Flat;

    private static void ValidateBaseCap(LineCap baseCap)
    {
        if (!IsBasicLineCap(baseCap))
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static void ValidateStrokeCap(LineCap cap)
    {
        if (!IsBasicLineCap(cap))
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static bool IsBasicLineCap(LineCap cap)
        => cap is LineCap.Flat or LineCap.Square or LineCap.Round or LineCap.Triangle;

    private static void ValidateFillPath(GraphicsPath? fillPath)
    {
        if (fillPath is null || fillPath.PointCount == 0)
        {
            return;
        }

        PointF[] points = fillPath.PathPoints;
        bool explicitlyClosed = (fillPath.PathTypes[^1] & (byte)PathPointType.CloseSubpath) != 0;
        bool returnsToStart = points.Length >= 3 && points[0] == points[^1];
        if (!explicitlyClosed && !returnsToStart)
        {
            throw Status.InvalidParameter.GetException();
        }

        bool touchesYAxis = false;
        foreach (PointF point in points)
        {
            touchesYAxis |= point.X == 0;
        }

        if (!touchesYAxis)
        {
            throw new NotImplementedException();
        }
    }
}

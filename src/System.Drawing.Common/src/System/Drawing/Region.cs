// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Drawing2D;

namespace System.Drawing;

public sealed unsafe class Region : MarshalByRefObject, IDisposable, IPointer<GpRegion>
{
    private RectangleF _bounds;
    private bool _isInfinite;
    private bool _isEmpty;
    private bool _disposed;

    internal GpRegion* NativeRegion { get; private set; }

    nint IPointer<GpRegion>.Pointer => (nint)NativeRegion;

    public Region()
    {
        MakeInfinite();
    }

    public Region(RectangleF rect)
    {
        _bounds = rect;
        _isEmpty = rect.Width <= 0 || rect.Height <= 0;
    }

    public Region(Rectangle rect) : this((RectangleF)rect)
    {
    }

    public Region(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        MakeInfinite();
    }

    public Region(RegionData rgnData)
    {
        ArgumentNullException.ThrowIfNull(rgnData);
        MakeInfinite();
    }

    internal Region(GpRegion* nativeRegion) => MakeInfinite();

    public static Region FromHrgn(IntPtr hrgn)
    {
        return new Region();
    }

    private void SetNativeRegion(GpRegion* nativeRegion)
    {
        if (nativeRegion is null)
            throw new ArgumentNullException(nameof(nativeRegion));

        NativeRegion = nativeRegion;
    }

    public Region Clone()
    {
        ThrowIfDisposed();
        return new Region
        {
            _bounds = _bounds,
            _isInfinite = _isInfinite,
            _isEmpty = _isEmpty,
        };
    }

    public void ReleaseHrgn(IntPtr regionHandle)
    {
        if (regionHandle == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(regionHandle));
        }

        ThrowIfDisposed();
    }

    public void Dispose()
    {
        _disposed = true;
        NativeRegion = null;

        GC.SuppressFinalize(this);
    }

    ~Region() => Dispose();

    public void MakeInfinite()
    {
        ThrowIfDisposed();
        _isInfinite = true;
        _isEmpty = false;
        _bounds = new RectangleF(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
    }

    public void MakeEmpty()
    {
        ThrowIfDisposed();
        _isInfinite = false;
        _isEmpty = true;
        _bounds = RectangleF.Empty;
    }

    public void Intersect(RectangleF rect)
    {
        ThrowIfDisposed();
        if (_isEmpty || rect.Width <= 0 || rect.Height <= 0)
        {
            MakeEmpty();
            return;
        }

        if (_isInfinite)
        {
            _bounds = rect;
            _isInfinite = false;
            return;
        }

        _bounds = RectangleF.Intersect(_bounds, rect);
        _isEmpty = _bounds.IsEmpty;
    }

    public void Intersect(Rectangle rect) => Intersect((RectangleF)rect);

    public void Intersect(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfDisposed();
    }

    public void Intersect(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        ThrowIfDisposed();
        Intersect(region._bounds);
    }

    public void Union(RectangleF rect)
    {
        ThrowIfDisposed();
        if (_isInfinite || rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (_isEmpty)
        {
            _bounds = rect;
            _isEmpty = false;
            return;
        }

        _bounds = RectangleF.Union(_bounds, rect);
    }

    public void Union(Rectangle rect) => Union((RectangleF)rect);

    public void Union(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        MakeInfinite();
    }

    public void Union(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        ThrowIfDisposed();
        if (region._isInfinite)
        {
            MakeInfinite();
            return;
        }

        Union(region._bounds);
    }

    public void Xor(RectangleF rect) => Union(rect);

    public void Xor(Rectangle rect) => Xor((RectangleF)rect);

    public void Xor(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        MakeInfinite();
    }

    public void Xor(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Union(region);
    }

    public void Exclude(RectangleF rect)
    {
        ThrowIfDisposed();
        if (!_isInfinite && rect.Contains(_bounds))
        {
            MakeEmpty();
        }
    }

    public void Exclude(Rectangle rect) => Exclude((RectangleF)rect);

    public void Exclude(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        ThrowIfDisposed();
    }

    public void Exclude(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Exclude(region._bounds);
    }

    public void Complement(RectangleF rect)
    {
        ThrowIfDisposed();
        _bounds = rect;
        _isInfinite = false;
        _isEmpty = rect.Width <= 0 || rect.Height <= 0;
    }

    public void Complement(Rectangle rect) => Complement((RectangleF)rect);

    public void Complement(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        MakeInfinite();
    }

    public void Complement(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        ThrowIfDisposed();
        _bounds = region._bounds;
        _isInfinite = region._isInfinite;
        _isEmpty = region._isEmpty;
    }

    public void Translate(float dx, float dy)
    {
        ThrowIfDisposed();
        if (!_isInfinite && !_isEmpty)
        {
            _bounds.Offset(dx, dy);
        }
    }

    public void Translate(int dx, int dy) => Translate((float)dx, dy);

    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        ThrowIfDisposed();
        if (_isInfinite || _isEmpty)
        {
            return;
        }

        PointF[] points =
        [
            new(_bounds.Left, _bounds.Top),
            new(_bounds.Right, _bounds.Top),
            new(_bounds.Right, _bounds.Bottom),
            new(_bounds.Left, _bounds.Bottom),
        ];
        matrix.TransformPoints(points);
        _bounds = BoundsFromPoints(points);
    }

    public RectangleF GetBounds(Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        ThrowIfDisposed();
        return _isInfinite ? g.VisibleClipBounds : _bounds;
    }

    public IntPtr GetHrgn(Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        ThrowIfDisposed();
        return IntPtr.Zero;
    }

    public bool IsEmpty(Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        ThrowIfDisposed();
        return _isEmpty;
    }

    public bool IsInfinite(Graphics g)
    {
        ArgumentNullException.ThrowIfNull(g);
        ThrowIfDisposed();
        return _isInfinite;
    }

    public bool Equals(Region region, Graphics g)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(g);
        ThrowIfDisposed();
        return _isInfinite == region._isInfinite && _isEmpty == region._isEmpty && _bounds.Equals(region._bounds);
    }

    public RegionData? GetRegionData()
    {
        ThrowIfDisposed();
        return new RegionData([]);
    }

    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y), null);

    public bool IsVisible(PointF point) => IsVisible(point, null);

    public bool IsVisible(float x, float y, Graphics? g) => IsVisible(new PointF(x, y), g);

    public bool IsVisible(PointF point, Graphics? g)
    {
        ThrowIfDisposed();
        return _isInfinite || (!_isEmpty && _bounds.Contains(point));
    }

    public bool IsVisible(float x, float y, float width, float height) => IsVisible(new RectangleF(x, y, width, height), null);

    public bool IsVisible(RectangleF rect) => IsVisible(rect, null);

    public bool IsVisible(float x, float y, float width, float height, Graphics? g) => IsVisible(new RectangleF(x, y, width, height), g);

    public bool IsVisible(RectangleF rect, Graphics? g)
    {
        ThrowIfDisposed();
        return _isInfinite || (!_isEmpty && _bounds.IntersectsWith(rect));
    }

    public bool IsVisible(int x, int y, Graphics? g) => IsVisible(new Point(x, y), g);

    public bool IsVisible(Point point) => IsVisible(point, null);

    public bool IsVisible(Point point, Graphics? g) => IsVisible((PointF)point, g);

    public bool IsVisible(int x, int y, int width, int height) => IsVisible(new Rectangle(x, y, width, height), null);

    public bool IsVisible(Rectangle rect) => IsVisible(rect, null);

    public bool IsVisible(int x, int y, int width, int height, Graphics? g) => IsVisible(new Rectangle(x, y, width, height), g);

    public bool IsVisible(Rectangle rect, Graphics? g) => IsVisible((RectangleF)rect, g);

    public RectangleF[] GetRegionScans(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        ThrowIfDisposed();
        if (_isEmpty)
        {
            return [];
        }

        if (_isInfinite)
        {
            return [new RectangleF(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity)];
        }

        RectangleF rect = _bounds;
        PointF[] points =
        [
            new(rect.Left, rect.Top),
            new(rect.Right, rect.Top),
            new(rect.Right, rect.Bottom),
            new(rect.Left, rect.Bottom),
        ];
        matrix.TransformPoints(points);
        return [BoundsFromPoints(points)];
    }

    private static RectangleF BoundsFromPoints(ReadOnlySpan<PointF> points)
    {
        if (points.IsEmpty)
        {
            return RectangleF.Empty;
        }

        float minX = points[0].X;
        float minY = points[0].Y;
        float maxX = points[0].X;
        float maxY = points[0].Y;

        for (int i = 1; i < points.Length; i++)
        {
            minX = MathF.Min(minX, points[i].X);
            minY = MathF.Min(minY, points[i].Y);
            maxX = MathF.Max(maxX, points[i].X);
            maxY = MathF.Max(maxY, points[i].Y);
        }

        return RectangleF.FromLTRB(minX, minY, maxX, maxY);
    }

    private void CheckStatus(Status status)
    {
        if (status != Status.Ok)
            throw status.GetException();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

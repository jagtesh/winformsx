// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace System.Drawing.Drawing2D;

public sealed unsafe class GraphicsPath : MarshalByRefObject, ICloneable, IDisposable
{
    internal GpPath* _nativePath;

    private const float Flatness = (float)2.0 / (float)3.0;

    private readonly List<PointF> _points = [];
    private readonly List<byte> _types = [];
    private FillMode _fillMode;
    private bool _newFigure = true;
    private bool _disposed;

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
    public GraphicsPath() : this(FillMode.Alternate) { }

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
    public GraphicsPath(FillMode fillMode)
    {
        ValidateFillMode(fillMode);
        _fillMode = fillMode;
    }

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
    public GraphicsPath(PointF[] pts, byte[] types) : this(pts, types, FillMode.Alternate) { }

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
    public GraphicsPath(PointF[] pts, byte[] types, FillMode fillMode)
        : this(AsSpan(pts), AsSpan(types), fillMode)
    {
    }

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    internal
#endif
    GraphicsPath(ReadOnlySpan<PointF> pts, ReadOnlySpan<byte> types, FillMode fillMode = FillMode.Alternate)
        : this(fillMode)
    {
        if (pts.Length != types.Length)
        {
            throw Status.InvalidParameter.GetException();
        }

        _points.AddRange(pts.ToArray());
        _types.AddRange(types.ToArray());
        _newFigure = _points.Count == 0 || IsClosed(_types[^1]);
    }

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
    public GraphicsPath(Point[] pts, byte[] types) : this(pts, types, FillMode.Alternate) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref='GraphicsPath'/> class.
    /// </summary>
    /// <param name="pts">Array of points that define the path.</param>
    /// <param name="types">Array of <see cref="PathPointType"/> values that specify the type of <paramref name="pts"/></param>
    /// <param name="fillMode">
    ///  A <see cref="Drawing2D.FillMode"/> enumeration that specifies how the interiors of shapes in this <see cref="GraphicsPath"/>
    /// </param>
    public GraphicsPath(Point[] pts, byte[] types, FillMode fillMode)
        : this(ToPointFSpan(pts), AsSpan(types), fillMode) { }

    /// <inheritdoc cref="GraphicsPath(Point[], byte[], FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    internal
#endif
    GraphicsPath(ReadOnlySpan<Point> pts, ReadOnlySpan<byte> types, FillMode fillMode = FillMode.Alternate)
        : this(ToPointFArray(pts), types, fillMode)
    {
    }

    public object Clone()
    {
        ThrowIfDisposed();
        GraphicsPath path = new(_fillMode)
        {
            _newFigure = _newFigure
        };

        path._points.AddRange(_points);
        path._types.AddRange(_types);
        return path;
    }

    private GraphicsPath(GpPath* nativePath)
    {
        if (nativePath is null)
            throw new ArgumentNullException(nameof(nativePath));

        _nativePath = null;
    }

    public void Dispose()
    {
        _disposed = true;
        _nativePath = null;
        GC.SuppressFinalize(this);
    }

    ~GraphicsPath() => Dispose();

    public void Reset()
    {
        ThrowIfDisposed();
        _points.Clear();
        _types.Clear();
        _newFigure = true;
    }

    public FillMode FillMode
    {
        get
        {
            ThrowIfDisposed();
            return _fillMode;
        }
        set
        {
            ValidateFillMode(value);
            ThrowIfDisposed();
            _fillMode = value;
        }
    }

    public PathData PathData
    {
        get
        {
            ThrowIfDisposed();
            return new PathData
            {
                Types = _types.ToArray(),
                Points = _points.ToArray()
            };
        }
    }

    public void StartFigure()
    {
        ThrowIfDisposed();
        _newFigure = true;
    }

    public void CloseFigure()
    {
        ThrowIfDisposed();
        CloseCurrentFigure();
    }

    public void CloseAllFigures()
    {
        ThrowIfDisposed();
        for (int i = 0; i < _types.Count; i++)
        {
            bool isLast = i == _types.Count - 1 || IsStart(_types[i + 1]);
            if (isLast)
            {
                _types[i] = (byte)(_types[i] | (byte)PathPointType.CloseSubpath);
            }
        }

        _newFigure = true;
    }

    public void SetMarkers()
    {
        ThrowIfDisposed();
        if (_types.Count > 0)
        {
            _types[^1] = (byte)(_types[^1] | (byte)PathPointType.PathMarker);
        }
    }

    public void ClearMarkers()
    {
        ThrowIfDisposed();
        for (int i = 0; i < _types.Count; i++)
        {
            _types[i] = (byte)(_types[i] & ~(byte)PathPointType.PathMarker);
        }
    }

    public void Reverse()
    {
        ThrowIfDisposed();
        _points.Reverse();
        _types.Reverse();
        if (_types.Count > 0)
        {
            _types[0] = (byte)((_types[0] & ~(byte)PathPointType.PathTypeMask) | (byte)PathPointType.Start);
        }
    }

    public PointF GetLastPoint()
    {
        ThrowIfDisposed();
        return _points.Count == 0 ? PointF.Empty : _points[^1];
    }

    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y), null);

    public bool IsVisible(PointF point) => IsVisible(point, null);

    public bool IsVisible(float x, float y, Graphics? graphics)
    {
        ThrowIfDisposed();
        return GetBounds().Contains(x, y);
    }

    public bool IsVisible(PointF pt, Graphics? graphics) => IsVisible(pt.X, pt.Y, graphics);

    public bool IsVisible(int x, int y) => IsVisible((float)x, y, null);

    public bool IsVisible(Point point) => IsVisible((PointF)point, null);

    public bool IsVisible(int x, int y, Graphics? graphics) => IsVisible((float)x, y, graphics);

    public bool IsVisible(Point pt, Graphics? graphics) => IsVisible((PointF)pt, graphics);

    public bool IsOutlineVisible(float x, float y, Pen pen) => IsOutlineVisible(new PointF(x, y), pen, null);

    public bool IsOutlineVisible(PointF point, Pen pen) => IsOutlineVisible(point, pen, null);

    public bool IsOutlineVisible(float x, float y, Pen pen, Graphics? graphics)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ThrowIfDisposed();

        RectangleF bounds = GetBounds();
        float inflate = Math.Max(1.0f, pen.Width) / 2.0f;
        bounds.Inflate(inflate, inflate);
        return bounds.Contains(x, y);
    }

    public bool IsOutlineVisible(PointF pt, Pen pen, Graphics? graphics) => IsOutlineVisible(pt.X, pt.Y, pen, graphics);

    public bool IsOutlineVisible(int x, int y, Pen pen) => IsOutlineVisible(new Point(x, y), pen, null);

    public bool IsOutlineVisible(Point point, Pen pen) => IsOutlineVisible(point, pen, null);

    public bool IsOutlineVisible(int x, int y, Pen pen, Graphics? graphics) => IsOutlineVisible((float)x, y, pen, graphics);

    public bool IsOutlineVisible(Point pt, Pen pen, Graphics? graphics) => IsOutlineVisible((PointF)pt, pen, graphics);

    public void AddLine(PointF pt1, PointF pt2) => AddLine(pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void AddLine(float x1, float y1, float x2, float y2)
    {
        ThrowIfDisposed();
        AppendConnectedStart(new PointF(x1, y1));
        AppendPoint(new PointF(x2, y2), PathPointType.Line);
    }

    public void AddLines(params PointF[] points) => AddLines(AsSpan(points));

    /// <inheritdoc cref="AddLines(PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddLines(params ReadOnlySpan<PointF> points)
    {
        ThrowIfDisposed();
        if (points.Length == 0)
        {
            throw new ArgumentException(null, nameof(points));
        }

        AppendConnectedStart(points[0]);
        for (int i = 1; i < points.Length; i++)
        {
            AppendPoint(points[i], PathPointType.Line);
        }
    }

    public void AddLine(Point pt1, Point pt2) => AddLine((float)pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void AddLine(int x1, int y1, int x2, int y2) => AddLine((float)x1, y1, x2, y2);

    public void AddLines(params Point[] points) => AddLines(ToPointFSpan(points));

    /// <inheritdoc cref="AddLines(PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddLines(params ReadOnlySpan<Point> points) => AddLines(ToPointFArray(points).AsSpan());

    public void AddArc(RectangleF rect, float startAngle, float sweepAngle) =>
        AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        ThrowIfDisposed();
        AddArcApproximation(new RectangleF(x, y, width, height), startAngle, sweepAngle);
    }

    public void AddArc(Rectangle rect, float startAngle, float sweepAngle) =>
        AddArc(rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddArc(int x, int y, int width, int height, float startAngle, float sweepAngle) =>
        AddArc((float)x, y, width, height, startAngle, sweepAngle);

    public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4) =>
        AddBezier(pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        ThrowIfDisposed();
        AppendConnectedStart(new PointF(x1, y1));
        AppendPoint(new PointF(x2, y2), PathPointType.Bezier3);
        AppendPoint(new PointF(x3, y3), PathPointType.Bezier3);
        AppendPoint(new PointF(x4, y4), PathPointType.Bezier3);
    }

    public void AddBeziers(params PointF[] points) => AddBeziers(AsSpan(points));

    /// <inheritdoc cref="AddBeziers(PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    internal
#endif
    void AddBeziers(params ReadOnlySpan<PointF> points)
    {
        ThrowIfDisposed();
        if (points.Length == 0)
            return;

        AppendConnectedStart(points[0]);
        for (int i = 1; i < points.Length; i++)
        {
            AppendPoint(points[i], PathPointType.Bezier3);
        }
    }

    public void AddBezier(Point pt1, Point pt2, Point pt3, Point pt4) =>
        AddBezier((float)pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    public void AddBezier(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4) =>
        AddBezier((float)x1, y1, x2, y2, x3, y3, x4, y4);

    public void AddBeziers(params Point[] points) => AddBeziers(ToPointFSpan(points));

    /// <inheritdoc cref="AddBeziers(PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    internal
#endif
    void AddBeziers(params ReadOnlySpan<Point> points) => AddBeziers(ToPointFArray(points).AsSpan());

    public void AddCurve(params PointF[] points) => AddCurve(AsSpan(points), 0.5f);

    public void AddCurve(PointF[] points, float tension) => AddCurve(AsSpan(points), tension);

    public void AddCurve(PointF[] points, int offset, int numberOfSegments, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        AddCurve(points.AsSpan(offset, numberOfSegments + 1), tension);
    }

#if NET9_0_OR_GREATER
    public void AddCurve(params ReadOnlySpan<PointF> points) => AddCurve(points, 0.5f);
#endif

    /// <inheritdoc cref="AddCurve(PointF[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddCurve(ReadOnlySpan<PointF> points, float tension) => AddLines(points);

    public void AddCurve(params Point[] points) => AddCurve(ToPointFSpan(points), 0.5f);

    public void AddCurve(Point[] points, float tension) => AddCurve(ToPointFSpan(points), tension);

    public void AddCurve(Point[] points, int offset, int numberOfSegments, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        AddCurve(ToPointFArray(points.AsSpan(offset, numberOfSegments + 1)).AsSpan(), tension);
    }

#if NET9_0_OR_GREATER
    public void AddCurve(ReadOnlySpan<Point> points) => AddCurve(points, 0.5f);
#endif

    /// <inheritdoc cref="AddCurve(PointF[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddCurve(ReadOnlySpan<Point> points, float tension) => AddCurve(ToPointFArray(points).AsSpan(), tension);

    public void AddClosedCurve(params PointF[] points) => AddClosedCurve(AsSpan(points), 0.5f);

    public void AddClosedCurve(PointF[] points, float tension) => AddClosedCurve(AsSpan(points), tension);

#if NET9_0_OR_GREATER
    public void AddClosedCurve(params ReadOnlySpan<PointF> points) => AddClosedCurve(points, 0.5f);
#endif

    /// <inheritdoc cref="AddClosedCurve(Point[], float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddClosedCurve(ReadOnlySpan<PointF> points, float tension)
    {
        AddLines(points);
        CloseFigure();
    }

    public void AddClosedCurve(params Point[] points) => AddClosedCurve(ToPointFSpan(points), 0.5f);

    public void AddClosedCurve(Point[] points, float tension) => AddClosedCurve(ToPointFSpan(points), tension);

#if NET9_0_OR_GREATER
    public void AddClosedCurve(params ReadOnlySpan<Point> points) => AddClosedCurve(points, 0.5f);
#endif

    /// <inheritdoc cref="AddClosedCurve(Point[], float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddClosedCurve(ReadOnlySpan<Point> points, float tension) => AddClosedCurve(ToPointFArray(points).AsSpan(), tension);

    public void AddRectangle(RectangleF rect)
    {
        ThrowIfDisposed();
        if (rect.Width == 0 || rect.Height == 0)
            return;

        StartFigure();
        AppendPoint(new PointF(rect.Left, rect.Top), PathPointType.Start);
        AppendPoint(new PointF(rect.Right, rect.Top), PathPointType.Line);
        AppendPoint(new PointF(rect.Right, rect.Bottom), PathPointType.Line);
        AppendPoint(new PointF(rect.Left, rect.Bottom), PathPointType.Line);
        CloseFigure();
    }

    public void AddRectangles(params RectangleF[] rects) => AddRectangles(AsSpan(rects));

    /// <inheritdoc cref="AddRectangles(RectangleF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddRectangles(params ReadOnlySpan<RectangleF> rects)
    {
        ThrowIfDisposed();
        foreach (RectangleF rect in rects)
        {
            AddRectangle(rect);
        }
    }

    public void AddRectangle(Rectangle rect) => AddRectangle((RectangleF)rect);

    public void AddRectangles(params Rectangle[] rects) => AddRectangles(ToRectangleFSpan(rects));

    /// <inheritdoc cref="AddRectangles(RectangleF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddRectangles(params ReadOnlySpan<Rectangle> rects) => AddRectangles(ToRectangleFArray(rects).AsSpan());

#if NET9_0_OR_GREATER
    public void AddRoundedRectangle(Rectangle rect, Size radius) =>
        AddRoundedRectangle((RectangleF)rect, radius);

    public void AddRoundedRectangle(RectangleF rect, SizeF radius)
    {
        StartFigure();
        AddArc(rect.Right - radius.Width, rect.Top, radius.Width, radius.Height, -90.0f, 90.0f);
        AddArc(rect.Right - radius.Width, rect.Bottom - radius.Height, radius.Width, radius.Height, 0.0f, 90.0f);
        AddArc(rect.Left, rect.Bottom - radius.Height, radius.Width, radius.Height, 90.0f, 90.0f);
        AddArc(rect.Left, rect.Top, radius.Width, radius.Height, 180.0f, 90.0f);
        CloseFigure();
    }
#endif

    public void AddEllipse(RectangleF rect) =>
        AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);

    public void AddEllipse(float x, float y, float width, float height)
    {
        ThrowIfDisposed();
        AddEllipseApproximation(new RectangleF(x, y, width, height));
    }

    public void AddEllipse(Rectangle rect) => AddEllipse(rect.X, rect.Y, rect.Width, rect.Height);

    public void AddEllipse(int x, int y, int width, int height) => AddEllipse((float)x, y, width, height);

    public void AddPie(Rectangle rect, float startAngle, float sweepAngle) =>
        AddPie((float)rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddPie(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        ThrowIfDisposed();
        RectangleF rect = new(x, y, width, height);
        StartFigure();
        AppendPoint(new PointF(rect.X + rect.Width / 2.0f, rect.Y + rect.Height / 2.0f), PathPointType.Start);
        AddArcApproximation(rect, startAngle, sweepAngle);
        CloseFigure();
    }

    public void AddPie(int x, int y, int width, int height, float startAngle, float sweepAngle) =>
        AddPie((float)x, y, width, height, startAngle, sweepAngle);

    public void AddPolygon(params PointF[] points) => AddPolygon(AsSpan(points));

    /// <inheritdoc cref="AddPolygon(Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddPolygon(params ReadOnlySpan<PointF> points)
    {
        AddLines(points);
        CloseFigure();
    }

    public void AddPolygon(params Point[] points) => AddPolygon(ToPointFSpan(points));

    /// <inheritdoc cref="AddPolygon(Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void AddPolygon(params ReadOnlySpan<Point> points) => AddPolygon(ToPointFArray(points).AsSpan());

    public void AddPath(GraphicsPath addingPath, bool connect)
    {
        ArgumentNullException.ThrowIfNull(addingPath);
        ThrowIfDisposed();
        addingPath.ThrowIfDisposed();

        for (int i = 0; i < addingPath._points.Count; i++)
        {
            byte type = addingPath._types[i];
            if (connect && i == 0 && _points.Count > 0)
            {
                type = (byte)((type & ~(byte)PathPointType.PathTypeMask) | (byte)PathPointType.Line);
            }

            _points.Add(addingPath._points[i]);
            _types.Add(type);
        }

        _newFigure = _points.Count == 0 || IsClosed(_types[^1]);
    }

    public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat? format) =>
        AddString(s, family, style, emSize, new RectangleF(origin.X, origin.Y, 0, 0), format);

    public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat? format) =>
        AddString(s, family, style, emSize, new Rectangle(origin.X, origin.Y, 0, 0), format);

    public void AddString(string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(family);
        ThrowIfDisposed();

        float width = layoutRect.Width > 0 ? layoutRect.Width : s.Length * emSize * 0.55f;
        float height = layoutRect.Height > 0 ? layoutRect.Height : emSize;
        AddRectangle(new RectangleF(layoutRect.X, layoutRect.Y, width, height));
    }

    public void AddString(string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat? format)
        => AddString(s, family, style, emSize, (RectangleF)layoutRect, format);

    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ThrowIfDisposed();
        if (_points.Count == 0)
            return;

        PointF[] points = _points.ToArray();
        matrix.TransformPoints(points);
        _points.Clear();
        _points.AddRange(points);
    }

    public RectangleF GetBounds() => GetBounds(null);

    public RectangleF GetBounds(Matrix? matrix) => GetBounds(matrix, null);

    public RectangleF GetBounds(Matrix? matrix, Pen? pen)
    {
        ThrowIfDisposed();
        if (_points.Count == 0)
        {
            return RectangleF.Empty;
        }

        PointF[] points = _points.ToArray();
        matrix?.TransformPoints(points);
        RectangleF bounds = BoundsFromPoints(points);
        if (pen is not null)
        {
            float inflate = Math.Max(0.0f, pen.Width) / 2.0f;
            bounds.Inflate(inflate, inflate);
        }

        return bounds;
    }

    public void Flatten() => Flatten(null);

    public void Flatten(Matrix? matrix) => Flatten(matrix, 0.25f);

    public void Flatten(Matrix? matrix, float flatness)
    {
        ThrowIfDisposed();
        if (matrix is not null)
        {
            Transform(matrix);
        }
    }

    public void Widen(Pen pen) => Widen(pen, null, Flatness);

    public void Widen(Pen pen, Matrix? matrix) => Widen(pen, matrix, Flatness);

    public void Widen(Pen pen, Matrix? matrix, float flatness)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ThrowIfDisposed();
        if (PointCount == 0)
            return;

        RectangleF bounds = GetBounds(matrix, pen);
        Reset();
        AddRectangle(bounds);
    }

    public void Warp(PointF[] destPoints, RectangleF srcRect) => Warp(destPoints, srcRect, null);

    public void Warp(PointF[] destPoints, RectangleF srcRect, Matrix? matrix) =>
        Warp(destPoints, srcRect, matrix, WarpMode.Perspective);

    public void Warp(PointF[] destPoints, RectangleF srcRect, Matrix? matrix, WarpMode warpMode) =>
        Warp(destPoints, srcRect, matrix, warpMode, 0.25f);

    public void Warp(PointF[] destPoints, RectangleF srcRect, Matrix? matrix, WarpMode warpMode, float flatness) =>
        Warp(AsSpan(destPoints), srcRect, matrix, warpMode, flatness);

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void Warp(
        ReadOnlySpan<PointF> destPoints,
        RectangleF srcRect,
        Matrix? matrix = default,
        WarpMode warpMode = WarpMode.Perspective,
        float flatness = 0.25f)
    {
        ThrowIfDisposed();
        if (destPoints.Length < 3)
        {
            throw Status.InvalidParameter.GetException();
        }

        RectangleF bounds = BoundsFromPoints(destPoints);
        Reset();
        AddRectangle(bounds);
        if (matrix is not null)
        {
            Transform(matrix);
        }
    }

    public int PointCount
    {
        get
        {
            ThrowIfDisposed();
            return _points.Count;
        }
    }

    public byte[] PathTypes
    {
        get
        {
            ThrowIfDisposed();
            return _types.ToArray();
        }
    }

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    int GetPathTypes(Span<byte> destination)
    {
        ThrowIfDisposed();
        int count = Math.Min(destination.Length, _types.Count);
        for (int i = 0; i < count; i++)
        {
            destination[i] = _types[i];
        }

        return count;
    }

    public PointF[] PathPoints
    {
        get
        {
            ThrowIfDisposed();
            return _points.ToArray();
        }
    }

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    int GetPathPoints(Span<PointF> destination)
    {
        ThrowIfDisposed();
        int count = Math.Min(destination.Length, _points.Count);
        for (int i = 0; i < count; i++)
        {
            destination[i] = _points[i];
        }

        return count;
    }

    private static ReadOnlySpan<T> AsSpan<T>(T[]? values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values;
    }

    private static ReadOnlySpan<PointF> ToPointFSpan(Point[]? points)
    {
        ArgumentNullException.ThrowIfNull(points);
        return ToPointFArray(points).AsSpan();
    }

    private static PointF[] ToPointFArray(ReadOnlySpan<Point> points)
    {
        PointF[] result = new PointF[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            result[i] = points[i];
        }

        return result;
    }

    private static ReadOnlySpan<RectangleF> ToRectangleFSpan(Rectangle[]? rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        return ToRectangleFArray(rects).AsSpan();
    }

    private static RectangleF[] ToRectangleFArray(ReadOnlySpan<Rectangle> rects)
    {
        RectangleF[] result = new RectangleF[rects.Length];
        for (int i = 0; i < rects.Length; i++)
        {
            result[i] = rects[i];
        }

        return result;
    }

    private void AppendConnectedStart(PointF point)
    {
        if (_points.Count == 0 || _newFigure)
        {
            AppendPoint(point, PathPointType.Start);
            return;
        }

        if (_points[^1] != point)
        {
            AppendPoint(point, PathPointType.Line);
        }
    }

    private void AppendPoint(PointF point, PathPointType type)
    {
        _points.Add(point);
        _types.Add((byte)type);
        _newFigure = false;
    }

    private void CloseCurrentFigure()
    {
        if (_types.Count == 0)
        {
            _newFigure = true;
            return;
        }

        _types[^1] = (byte)(_types[^1] | (byte)PathPointType.CloseSubpath);
        _newFigure = true;
    }

    private void AddEllipseApproximation(RectangleF rect)
    {
        if (rect.Width == 0 || rect.Height == 0)
            return;

        const int segments = 24;
        StartFigure();
        for (int i = 0; i < segments; i++)
        {
            float radians = (float)(i * Math.Tau / segments);
            PointF point = new(
                rect.X + rect.Width / 2.0f + MathF.Cos(radians) * rect.Width / 2.0f,
                rect.Y + rect.Height / 2.0f + MathF.Sin(radians) * rect.Height / 2.0f);
            AppendPoint(point, i == 0 ? PathPointType.Start : PathPointType.Line);
        }

        CloseFigure();
    }

    private void AddArcApproximation(RectangleF rect, float startAngle, float sweepAngle)
    {
        if (rect.Width == 0 || rect.Height == 0)
            return;

        int segments = Math.Max(2, (int)MathF.Ceiling(MathF.Abs(sweepAngle) / 15.0f));
        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + sweepAngle * i / segments;
            float radians = angle * MathF.PI / 180.0f;
            PointF point = new(
                rect.X + rect.Width / 2.0f + MathF.Cos(radians) * rect.Width / 2.0f,
                rect.Y + rect.Height / 2.0f + MathF.Sin(radians) * rect.Height / 2.0f);

            if (i == 0)
            {
                AppendConnectedStart(point);
            }
            else
            {
                AppendPoint(point, PathPointType.Line);
            }
        }
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

    private static bool IsStart(byte type) => (type & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Start;

    private static bool IsClosed(byte type) => (type & (byte)PathPointType.CloseSubpath) != 0;

    private static void ValidateFillMode(FillMode fillMode)
    {
        if (fillMode is < FillMode.Alternate or > FillMode.Winding)
            throw new InvalidEnumArgumentException(nameof(fillMode), (int)fillMode, typeof(FillMode));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

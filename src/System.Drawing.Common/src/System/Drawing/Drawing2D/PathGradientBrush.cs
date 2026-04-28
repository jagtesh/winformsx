// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace System.Drawing.Drawing2D;

/// <summary>
///  Encapsulates a <see cref="Brush"/> object that fills the interior of a <see cref="GraphicsPath"/> object with a gradient.
/// </summary>
public sealed unsafe class PathGradientBrush : Brush
{
    private PointF[] _points;
    private Color _centerColor = Color.White;
    private Color[] _surroundColors;
    private PointF _centerPoint;
    private RectangleF _rectangle;
    private Blend _blend = new();
    private ColorBlend _interpolationColors = new();
    private Matrix _transform = new();
    private PointF _focusScales = new(1.0f, 1.0f);
    private WrapMode _wrapMode;

    public PathGradientBrush(params PointF[] points) : this(points, WrapMode.Clamp) { }

#if NET9_0_OR_GREATER
    public PathGradientBrush(params ReadOnlySpan<PointF> points) : this(WrapMode.Clamp, points) { }
#endif

    public PathGradientBrush(PointF[] points, WrapMode wrapMode) : this(wrapMode, AsSpan(points)) { }

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    PathGradientBrush(WrapMode wrapMode, params ReadOnlySpan<PointF> points)
    {
        ValidateWrapMode(wrapMode);
        if (points.Length < 2)
            throw new ArgumentException(null, nameof(points));

        _points = points.ToArray();
        _wrapMode = wrapMode;
        _surroundColors = CreateColorArray(Color.White, points.Length);
        _rectangle = BoundsFromPoints(points);
        _centerPoint = new PointF(_rectangle.X + _rectangle.Width / 2.0f, _rectangle.Y + _rectangle.Height / 2.0f);
    }

    public PathGradientBrush(params Point[] points) : this(points, WrapMode.Clamp) { }

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    PathGradientBrush(params ReadOnlySpan<Point> points) : this(WrapMode.Clamp, points) { }

    public PathGradientBrush(Point[] points, WrapMode wrapMode) : this(wrapMode, AsSpan(points)) { }

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    PathGradientBrush(WrapMode wrapMode, params ReadOnlySpan<Point> points)
        : this(wrapMode, ToPointFArray(points).AsSpan())
    {
    }

    public PathGradientBrush(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        PointF[] points = path.PathPoints;
        if (points.Length < 2)
        {
            RectangleF bounds = path.GetBounds();
            points =
            [
                new(bounds.Left, bounds.Top),
                new(bounds.Right, bounds.Top),
                new(bounds.Right, bounds.Bottom),
                new(bounds.Left, bounds.Bottom)
            ];
        }

        _points = points;
        _wrapMode = WrapMode.Clamp;
        _surroundColors = CreateColorArray(Color.White, points.Length);
        _rectangle = BoundsFromPoints(points);
        _centerPoint = new PointF(_rectangle.X + _rectangle.Width / 2.0f, _rectangle.Y + _rectangle.Height / 2.0f);
    }

    internal PathGradientBrush(GpPathGradient* nativeBrush)
    {
        Debug.Assert(nativeBrush is not null, "Initializing native brush with null.");
        _points = [PointF.Empty, new PointF(1, 0)];
        _surroundColors = [Color.White, Color.White];
        _rectangle = new RectangleF(0, 0, 1, 1);
    }

    internal GpPathGradient* NativePathGradient => null;

    public override object Clone()
    {
        PathGradientBrush clone = new(_points, _wrapMode)
        {
            _centerColor = _centerColor,
            _surroundColors = (Color[])_surroundColors.Clone(),
            _centerPoint = _centerPoint,
            _rectangle = _rectangle,
            _blend = CloneBlend(_blend),
            _interpolationColors = CloneColorBlend(_interpolationColors),
            _transform = (Matrix)_transform.Clone(),
            _focusScales = _focusScales
        };

        return clone;
    }

    public Color CenterColor
    {
        get => _centerColor;
        set => _centerColor = value;
    }

    public Color[] SurroundColors
    {
        get => (Color[])_surroundColors.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _surroundColors = (Color[])value.Clone();
        }
    }

    public PointF CenterPoint
    {
        get => _centerPoint;
        set => _centerPoint = value;
    }

    public RectangleF Rectangle => _rectangle;

    public Blend Blend
    {
        get => CloneBlend(_blend);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateBlend(value);
            _blend = CloneBlend(value);
        }
    }

    public void SetSigmaBellShape(float focus) => SetSigmaBellShape(focus, (float)1.0);

    public void SetSigmaBellShape(float focus, float scale)
    {
        _blend = new Blend(3)
        {
            Factors = [0.0f, scale, 0.0f],
            Positions = [0.0f, focus, 1.0f]
        };
    }

    public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, (float)1.0);

    public void SetBlendTriangularShape(float focus, float scale)
    {
        _blend = new Blend(3)
        {
            Factors = [0.0f, scale, 0.0f],
            Positions = [0.0f, focus, 1.0f]
        };
    }

    public ColorBlend InterpolationColors
    {
        get => CloneColorBlend(_interpolationColors);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateColorBlend(value);
            _interpolationColors = CloneColorBlend(value);
        }
    }

    public Matrix Transform
    {
        get => (Matrix)_transform.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _transform = (Matrix)value.Clone();
        }
    }

    public void ResetTransform() => _transform.Reset();

    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        _transform.Multiply(matrix, order);
    }

    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform.Translate(dx, dy, order);

    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    public void ScaleTransform(float sx, float sy, MatrixOrder order) => _transform.Scale(sx, sy, order);

    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    public void RotateTransform(float angle, MatrixOrder order) => _transform.Rotate(angle, order);

    public PointF FocusScales
    {
        get => _focusScales;
        set => _focusScales = value;
    }

    public WrapMode WrapMode
    {
        get => _wrapMode;
        set
        {
            ValidateWrapMode(value);
            _wrapMode = value;
        }
    }

    private static ReadOnlySpan<T> AsSpan<T>(T[]? values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values;
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

    private static Color[] CreateColorArray(Color color, int length)
    {
        Color[] result = new Color[length];
        Array.Fill(result, color);
        return result;
    }

    private static RectangleF BoundsFromPoints(ReadOnlySpan<PointF> points)
    {
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

    private static void ValidateWrapMode(WrapMode value)
    {
        if (value is < WrapMode.Tile or > WrapMode.Clamp)
            throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(WrapMode));
    }

    private static void ValidateBlend(Blend value)
    {
        ArgumentNullException.ThrowIfNull(value.Factors);
        if (value.Positions is null || value.Positions.Length != value.Factors.Length)
            throw new ArgumentException(SR.Format(SR.InvalidArgumentValue, "value.Positions", value.Positions), nameof(value));
    }

    private static void ValidateColorBlend(ColorBlend value)
    {
        if (value.Positions is null || value.Colors.Length != value.Positions.Length)
            throw new ArgumentException(SR.Format(SR.InvalidArgumentValue, "value.Positions", value.Positions), nameof(value));
    }

    private static Blend CloneBlend(Blend blend)
    {
        return new Blend(blend.Factors.Length)
        {
            Factors = (float[])blend.Factors.Clone(),
            Positions = (float[])blend.Positions.Clone()
        };
    }

    private static ColorBlend CloneColorBlend(ColorBlend blend)
    {
        return new ColorBlend(blend.Colors.Length)
        {
            Colors = (Color[])blend.Colors.Clone(),
            Positions = (float[])blend.Positions.Clone()
        };
    }
}

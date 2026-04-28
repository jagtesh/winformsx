// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace System.Drawing.Drawing2D;

public sealed unsafe class LinearGradientBrush : Brush
{
    private Color[] _linearColors = [Color.Empty, Color.Empty];
    private RectangleF _rectangle;
    private bool _gammaCorrection;
    private Blend? _blend;
    private ColorBlend _interpolationColors = new();
    private WrapMode _wrapMode = WrapMode.Tile;
    private Matrix _transform = new();
    private bool _interpolationColorsWasSet;

    public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
    {
        _linearColors = [color1, color2];
        _rectangle = RectangleF.FromLTRB(
            MathF.Min(point1.X, point2.X),
            MathF.Min(point1.Y, point2.Y),
            MathF.Max(point1.X, point2.X),
            MathF.Max(point1.Y, point2.Y));
    }

    public LinearGradientBrush(Point point1, Point point2, Color color1, Color color2)
        : this((PointF)point1, (PointF)point2, color1, color2)
    {
    }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
    {
        ValidateGradientMode(linearGradientMode);
        ValidateRectangle(rect);
        _rectangle = rect;
        _linearColors = [color1, color2];
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this((RectangleF)rect, color1, color2, linearGradientMode)
    {
    }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
        : this(rect, color1, color2, angle, isAngleScaleable: false)
    {
    }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle, bool isAngleScaleable)
    {
        ValidateRectangle(rect);
        _rectangle = rect;
        _linearColors = [color1, color2];
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
        : this(rect, color1, color2, angle, isAngleScaleable: false)
    {
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this((RectangleF)rect, color1, color2, angle, isAngleScaleable)
    {
    }

    internal LinearGradientBrush(GpLineGradient* nativeBrush)
    {
        Debug.Assert(nativeBrush is not null, "Initializing native brush with null.");
        _linearColors = [Color.Black, Color.White];
    }

    internal GpLineGradient* NativeLineGradient => null;

    public override object Clone()
    {
        LinearGradientBrush clone = new(_rectangle, _linearColors[0], _linearColors[1], 0.0f)
        {
            _gammaCorrection = _gammaCorrection,
            _wrapMode = _wrapMode,
            _transform = (Matrix)_transform.Clone(),
            _interpolationColorsWasSet = _interpolationColorsWasSet
        };

        clone._blend = CloneBlend(_blend);
        clone._interpolationColors = CloneColorBlend(_interpolationColors);
        return clone;
    }

    public Color[] LinearColors
    {
        get => (Color[])_linearColors.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length < 2)
                throw new ArgumentException(null, nameof(value));

            _linearColors = [value[0], value[1]];
        }
    }

    public RectangleF Rectangle => _rectangle;

    public bool GammaCorrection
    {
        get => _gammaCorrection;
        set => _gammaCorrection = value;
    }

    public Blend? Blend
    {
        get => _interpolationColorsWasSet ? null : CloneBlend(_blend);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateBlend(value);
            _blend = CloneBlend(value);
            _interpolationColorsWasSet = false;
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
        _interpolationColorsWasSet = false;
    }

    public void SetBlendTriangularShape(float focus) => SetBlendTriangularShape(focus, (float)1.0);

    public void SetBlendTriangularShape(float focus, float scale)
    {
        _blend = new Blend(3)
        {
            Factors = [0.0f, scale, 0.0f],
            Positions = [0.0f, focus, 1.0f]
        };
        _interpolationColorsWasSet = false;
    }

    public ColorBlend InterpolationColors
    {
        get => CloneColorBlend(_interpolationColors);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ValidateColorBlend(value);
            _interpolationColors = CloneColorBlend(value);
            _interpolationColorsWasSet = true;
        }
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

    private static void ValidateGradientMode(LinearGradientMode mode)
    {
        if (mode is < LinearGradientMode.Horizontal or > LinearGradientMode.BackwardDiagonal)
            throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(LinearGradientMode));
    }

    private static void ValidateRectangle(RectangleF rect)
    {
        if (rect.Width == 0.0 || rect.Height == 0.0)
            throw new ArgumentException(SR.Format(SR.GdiplusInvalidRectangle, rect.ToString()));
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

    private static Blend? CloneBlend(Blend? blend)
    {
        if (blend is null)
            return null;

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

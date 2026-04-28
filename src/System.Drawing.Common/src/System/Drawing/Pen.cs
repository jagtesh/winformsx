// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Internal;

namespace System.Drawing;

/// <summary>
///  Defines an object used to draw lines and curves.
/// </summary>
public sealed unsafe class Pen : MarshalByRefObject, ICloneable, IDisposable, ISystemColorTracker
{
    // Handle to native GDI+ pen object.
    private GpPen* _nativePen;

    // GDI+ doesn't understand system colors, so we need to cache the value here.
    private Color _color;
    private Brush? _brush;
    private float _width = 1.0f;
    private LineCap _startCap = LineCap.Flat;
    private LineCap _endCap = LineCap.Flat;
    private DashCap _dashCap = DashCap.Flat;
    private LineJoin _lineJoin = LineJoin.Miter;
    private float _miterLimit = 10.0f;
    private PenAlignment _alignment = PenAlignment.Center;
    private Matrix _transform = new();
    private DashStyle _dashStyle = DashStyle.Solid;
    private float _dashOffset;
    private float[] _dashPattern = [];
    private float[] _compoundArray = [];
    private bool _immutable;

    // Tracks whether the dash style has been changed to something else than Solid during the lifetime of this object.
    private bool _dashStyleWasOrIsNotSolid;

    /// <summary>
    ///  Creates a Pen from a native GDI+ object.
    /// </summary>
    private Pen(GpPen* nativePen) => SetNativePen(nativePen);

    internal Pen(Color color, bool immutable) : this(color) => _immutable = immutable;

    /// <summary>
    ///  Initializes a new instance of the Pen class with the specified <see cref='Color'/>.
    /// </summary>
    public Pen(Color color) : this(color, (float)1.0)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref='Pen'/> class with the specified
    ///  <see cref='Color'/> and <see cref='Width'/>.
    /// </summary>
    public Pen(Color color, float width)
    {
        _color = color;
        _brush = new SolidBrush(color);
        _width = width;

        if (_color.IsSystemColor)
        {
            SystemColorTracker.Add(this);
        }
    }

    /// <summary>
    ///  Initializes a new instance of the Pen class with the specified <see cref='Brush'/>.
    /// </summary>
    public Pen(Brush brush) : this(brush, (float)1.0)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref='Pen'/> class with the specified <see cref='Drawing.Brush'/> and width.
    /// </summary>
    public Pen(Brush brush, float width)
    {
        ArgumentNullException.ThrowIfNull(brush);
        _brush = (Brush)brush.Clone();
        _width = width;
        _color = brush is SolidBrush sb ? sb.Color : Color.Black;
    }

    internal void SetNativePen(GpPen* nativePen)
    {
        Debug.Assert(nativePen is not null);
        _nativePen = nativePen;
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    internal GpPen* NativePen => _nativePen;

    /// <summary>
    ///  Creates an exact copy of this <see cref='Pen'/>.
    /// </summary>
    public object Clone()
    {
        Pen clone = new(_brush ?? new SolidBrush(_color), _width)
        {
            _startCap = _startCap,
            _endCap = _endCap,
            _dashCap = _dashCap,
            _lineJoin = _lineJoin,
            _miterLimit = _miterLimit,
            _alignment = _alignment,
            _transform = (Matrix)_transform.Clone(),
            _dashStyle = _dashStyle,
            _dashOffset = _dashOffset,
            _dashPattern = (float[])_dashPattern.Clone(),
            _compoundArray = (float[])_compoundArray.Clone(),
            _dashStyleWasOrIsNotSolid = _dashStyleWasOrIsNotSolid,
        };

        return clone;
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='Pen'/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            // If we are finalizing, then we will be unreachable soon. Finalize calls dispose to
            // release resources, so we must make sure that during finalization we are
            // not immutable.
            _immutable = false;
        }
        else if (_immutable)
        {
            throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
        }

        _nativePen = null;
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='Pen'/>.
    /// </summary>
    ~Pen() => Dispose(disposing: false);

    /// <summary>
    ///  Gets or sets the width of this <see cref='Pen'/>.
    /// </summary>
    public float Width
    {
        get
        {
            return _width;
        }
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _width = value;
        }
    }

    /// <summary>
    ///  Sets the values that determine the style of cap used to end lines drawn by this <see cref='Pen'/>.
    /// </summary>
    public void SetLineCap(LineCap startCap, LineCap endCap, DashCap dashCap)
    {
        if (_immutable)
        {
            throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
        }

        _startCap = startCap;
        _endCap = endCap;
        _dashCap = dashCap;
    }

    /// <summary>
    ///  Gets or sets the cap style used at the beginning of lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public LineCap StartCap
    {
        get => _startCap;
        set
        {
            switch (value)
            {
                case LineCap.Flat:
                case LineCap.Square:
                case LineCap.Round:
                case LineCap.Triangle:
                case LineCap.NoAnchor:
                case LineCap.SquareAnchor:
                case LineCap.RoundAnchor:
                case LineCap.DiamondAnchor:
                case LineCap.ArrowAnchor:
                case LineCap.AnchorMask:
                case LineCap.Custom:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(LineCap));
            }

            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _startCap = value;
        }
    }

    /// <summary>
    ///  Gets or sets the cap style used at the end of lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public LineCap EndCap
    {
        get => _endCap;
        set
        {
            switch (value)
            {
                case LineCap.Flat:
                case LineCap.Square:
                case LineCap.Round:
                case LineCap.Triangle:
                case LineCap.NoAnchor:
                case LineCap.SquareAnchor:
                case LineCap.RoundAnchor:
                case LineCap.DiamondAnchor:
                case LineCap.ArrowAnchor:
                case LineCap.AnchorMask:
                case LineCap.Custom:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(LineCap));
            }

            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _endCap = value;
        }
    }

    /// <summary>
    ///  Gets or sets a custom cap style to use at the beginning of lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public CustomLineCap CustomStartCap
    {
        get => null!;
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

        }
    }

    /// <summary>
    ///  Gets or sets a custom cap style to use at the end of lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public CustomLineCap CustomEndCap
    {
        get => null!;
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

        }
    }

    /// <summary>
    ///  Gets or sets the cap style used at the beginning or end of dashed lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public DashCap DashCap
    {
        get => _dashCap;
        set
        {
            if (value is not DashCap.Flat and not DashCap.Round and not DashCap.Triangle)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DashCap));
            }

            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _dashCap = value;
        }
    }

    /// <summary>
    ///  Gets or sets the join style for the ends of two overlapping lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public LineJoin LineJoin
    {
        get => _lineJoin;
        set
        {
            if (value is < LineJoin.Miter or > LineJoin.MiterClipped)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(LineJoin));
            }

            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _lineJoin = value;
        }
    }

    /// <summary>
    ///  Gets or sets the limit of the thickness of the join on a mitered corner.
    /// </summary>
    public float MiterLimit
    {
        get => _miterLimit;
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _miterLimit = value;
        }
    }

    /// <summary>
    ///  Gets or sets the alignment for objects drawn with this <see cref='Pen'/>.
    /// </summary>
    public PenAlignment Alignment
    {
        get => _alignment;
        set
        {
            if (value is < PenAlignment.Center or > PenAlignment.Right)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(PenAlignment));
            }

            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _alignment = value;
        }
    }

    /// <summary>
    ///  Gets or sets the geometrical transform for objects drawn with this <see cref='Pen'/>.
    /// </summary>
    public Matrix Transform
    {
        get => (Matrix)_transform.Clone();
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            ArgumentNullException.ThrowIfNull(value);
            _transform = (Matrix)value.Clone();
        }
    }

    /// <summary>
    ///  Resets the geometric transform for this <see cref='Pen'/> to identity.
    /// </summary>
    public void ResetTransform()
    {
        _transform.Reset();
    }

    /// <summary>
    ///  Multiplies the transform matrix for this <see cref='Pen'/> by the specified <see cref='Matrix'/>.
    /// </summary>
    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    /// <summary>
    ///  Multiplies the transform matrix for this <see cref='Pen'/> by the specified <see cref='Matrix'/> in the specified order.
    /// </summary>
    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        _transform.Multiply(matrix, order);
    }

    /// <summary>
    ///  Translates the local geometrical transform by the specified dimensions. This method prepends the translation
    ///  to the transform.
    /// </summary>
    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    /// <summary>
    ///  Translates the local geometrical transform by the specified dimensions in the specified order.
    /// </summary>
    public void TranslateTransform(float dx, float dy, MatrixOrder order)
    {
        _transform.Translate(dx, dy, order);
    }

    /// <summary>
    ///  Scales the local geometric transform by the specified amounts. This method prepends the scaling matrix to the transform.
    /// </summary>
    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    /// <summary>
    ///  Scales the local geometric transform by the specified amounts in the specified order.
    /// </summary>
    public void ScaleTransform(float sx, float sy, MatrixOrder order)
    {
        _transform.Scale(sx, sy, order);
    }

    /// <summary>
    ///  Rotates the local geometric transform by the specified amount. This method prepends the rotation to the transform.
    /// </summary>
    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    /// <summary>
    ///  Rotates the local geometric transform by the specified amount in the specified order.
    /// </summary>
    public void RotateTransform(float angle, MatrixOrder order)
    {
        _transform.Rotate(angle, order);
    }

    private void InternalSetColor(Color value)
    {
        _color = value;
        _brush = new SolidBrush(value);
    }

    /// <summary>
    ///  Gets the style of lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public Drawing2D.PenType PenType
    {
        get
        {
            return _brush is SolidBrush ? Drawing2D.PenType.SolidColor : Drawing2D.PenType.TextureFill;
        }
    }

    /// <summary>
    ///  Gets or sets the color of this <see cref='Pen'/>.
    /// </summary>
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
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            if (value != _color)
            {
                Color oldColor = _color;
                _color = value;
                InternalSetColor(value);

                // NOTE: We never remove pens from the active list, so if someone is
                // changing their pen colors a lot, this could be a problem.
                if (value.IsSystemColor && !oldColor.IsSystemColor)
                {
                    SystemColorTracker.Add(this);
                }
            }
        }
    }

    /// <summary>
    ///  Gets or sets the <see cref='Drawing.Brush'/> that determines attributes of this <see cref='Pen'/>.
    /// </summary>
    public Brush Brush
    {
        get
        {
            return (Brush)(_brush ?? new SolidBrush(_color)).Clone();
        }
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            ArgumentNullException.ThrowIfNull(value);
            _brush = (Brush)value.Clone();
            _color = value is SolidBrush solidBrush ? solidBrush.Color : Color.Black;
        }
    }

    private GpBrush* GetNativeBrush()
    {
        return null;
    }

    /// <summary>
    ///  Gets or sets the style used for dashed lines drawn with this <see cref='Pen'/>.
    /// </summary>
    public DashStyle DashStyle
    {
        get => _dashStyle;
        set
        {
            if (value is < DashStyle.Solid or > DashStyle.Custom)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DashStyle));
            }

            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _dashStyle = value;

            // If we just set the pen style to Custom without defining the custom dash pattern,
            // make sure that we can return a valid value.
            if (value == DashStyle.Custom)
            {
                EnsureValidDashPattern();
            }

            if (value != DashStyle.Solid)
            {
                _dashStyleWasOrIsNotSolid = true;
            }
        }
    }

    /// <summary>
    ///  This method is called after the user sets the pen's dash style to custom. Here, we make sure that there
    ///  is a default value set for the custom pattern.
    /// </summary>
    private void EnsureValidDashPattern()
    {
        if (_dashPattern.Length == 0)
        {
            // Set to a solid pattern.
            DashPattern = [1];
        }
    }

    /// <summary>
    ///  Gets or sets the distance from the start of a line to the beginning of a dash pattern.
    /// </summary>
    public float DashOffset
    {
        get => _dashOffset;
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            _dashOffset = value;
        }
    }

    /// <summary>
    ///  Gets or sets an array of custom dashes and spaces. The dashes are made up of line segments.
    /// </summary>
    public float[] DashPattern
    {
        get
        {
            if (_dashPattern.Length == 0 && DashStyle == DashStyle.Solid && !_dashStyleWasOrIsNotSolid)
            {
                // Most likely we're replicating an existing System.Drawing bug here, it doesn't make much sense to
                // ask for a dash pattern when using a solid dash.
                throw new InvalidOperationException();
            }

            return _dashPattern.Length == 0 && DashStyle != DashStyle.Solid
                ? [1.0f]
                : (float[])_dashPattern.Clone();
        }
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            if (value is null || value.Length == 0)
            {
                throw new ArgumentException(SR.InvalidDashPattern);
            }

            _dashPattern = (float[])value.Clone();
        }
    }

    /// <summary>
    ///  Gets or sets an array of custom dashes and spaces. The dashes are made up of line segments.
    /// </summary>
    public float[] CompoundArray
    {
        get
        {
            return (float[])_compoundArray.Clone();
        }
        set
        {
            if (_immutable)
            {
                throw new ArgumentException(SR.Format(SR.CantChangeImmutableObjects, nameof(Pen)));
            }

            ArgumentNullException.ThrowIfNull(value);
            _compoundArray = (float[])value.Clone();
        }
    }

    void ISystemColorTracker.OnSystemColorChanged()
    {
        InternalSetColor(_color);
    }
}

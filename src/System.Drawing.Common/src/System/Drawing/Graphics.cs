// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
#if NET9_0_OR_GREATER
using System.Drawing.Imaging.Effects;
#endif
using System.Numerics;
using System.Runtime.Versioning;

namespace System.Drawing;

/// <summary>
///  Encapsulates a GDI+ drawing surface.
/// </summary>
public sealed unsafe partial class Graphics : MarshalByRefObject, IDisposable, IDeviceContext, IGraphics
{
    private bool IsBackendOnly => _backend is not null && NativeGraphics is null;
    /// <summary>
    ///  The context state previous to the current Graphics context (the head of the stack).
    ///  We don't keep a GraphicsContext for the current context since it is available at any time from GDI+ and
    ///  we don't want to keep track of changes in it.
    /// </summary>
    private GraphicsContext? _previousContext;

    /// <summary>
    ///  Custom hardware-accelerated rendering backend (e.g. Impeller). If set, drawing commands will route here.
    /// </summary>
    internal IRenderingBackend? _backend;

    /// <summary>
    ///  Optional factory to produce a backend for a given HWND.
    /// </summary>
    internal static Func<IntPtr, IRenderingBackend?>? BackendFactory { get; set; }

    /// <summary>
    ///  Returns true when a rendering backend (Impeller) is active.
    /// </summary>
    public static bool IsBackendActive => BackendFactory is not null;

    private static readonly object s_syncObject = new();

    // Object reference used for printing; it could point to a PrintPreviewGraphics to obtain the VisibleClipBounds, or
    // a DeviceContext holding a printer DC.
    private object? _printingHelper;

    // GDI+'s preferred HPALETTE.
    private static HPALETTE s_halftonePalette;

    // pointer back to the Image backing a specific graphic object
    private Image? _backingImage;
    private Region? _clip;
    private RectangleF _clipBounds = new(0, 0, int.MaxValue, int.MaxValue);
    private Drawing2D.CompositingMode _compositingMode = Drawing2D.CompositingMode.SourceOver;
    private Drawing2D.CompositingQuality _compositingQuality = Drawing2D.CompositingQuality.Default;
    private Drawing2D.InterpolationMode _interpolationMode = Drawing2D.InterpolationMode.Bilinear;
    private float _pageScale = 1f;
    private GraphicsUnit _pageUnit = GraphicsUnit.Display;
    private PixelOffsetMode _pixelOffsetMode = PixelOffsetMode.Default;
    private Point _renderingOrigin;
    private Drawing2D.SmoothingMode _smoothingMode = Drawing2D.SmoothingMode.Default;
    private int _textContrast;
    private TextRenderingHint _textRenderingHint = TextRenderingHint.SystemDefault;
    private Matrix _transform = new();
    private Matrix3x2 _transformElements = Matrix3x2.Identity;
    private int _nextContextState = 1;
    private bool _disposed;

    /// <summary>
    ///  Handle to native DC - obtained from the GDI+ graphics object. We need to cache it to implement
    ///  IDeviceContext interface.
    /// </summary>
    private HDC _nativeHdc;

    public delegate bool DrawImageAbort(IntPtr callbackdata);

    private static NotSupportedException GdiPlusRemoved(string api) =>
        new($"{api} requires a Drawing PAL implementation. WinFormsX is Impeller-only and cannot use native GDI+.");

    private static Color BrushColor(Brush brush) => brush switch
    {
        SolidBrush solidBrush => solidBrush.Color,
        Drawing2D.LinearGradientBrush linearGradientBrush => linearGradientBrush.LinearColors[0],
        _ => Color.Transparent,
    };

    /// <summary>
    /// Callback for EnumerateMetafile methods.
    /// This method can then call Metafile.PlayRecord to play the record that was just enumerated.
    /// </summary>
    /// <param name="recordType">if >= MinRecordType, it's an EMF+ record</param>
    /// <param name="flags">always 0 for EMF records</param>
    /// <param name="dataSize">size of the data, or 0 if no data</param>
    /// <param name="data">pointer to the data, or NULL if no data (UINT32 aligned)</param>
    /// <param name="callbackData">pointer to callbackData, if any</param>
    /// <returns>False to abort enumerating, true to continue.</returns>
    public delegate bool EnumerateMetafileProc(
        EmfPlusRecordType recordType,
        int flags,
        int dataSize,
        IntPtr data,
        PlayRecordCallback? callbackData);

    /// <summary>
    ///  Constructor to initialize this object from a native GDI+ Graphics pointer.
    /// </summary>
    private Graphics(GpGraphics* gdipNativeGraphics)
    {
        if (gdipNativeGraphics is null)
            throw new ArgumentNullException(nameof(gdipNativeGraphics));

        NativeGraphics = gdipNativeGraphics;
    }

    /// <summary>
    ///  Backend-only constructor. No GDI+ surface is created; rendering is fully delegated
    ///  to <see cref="_backend"/>.
    /// </summary>
    private Graphics(IRenderingBackend backend)
    {
        _backend = backend;
        NativeGraphics = null;
    }

    /// <summary>
    ///  Creates a new instance of the <see cref='Graphics'/> class from the specified handle to a device context.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Graphics FromHdc(IntPtr hdc)
    {
        if (hdc == IntPtr.Zero)
            throw new ArgumentNullException(nameof(hdc));

        return FromHdcInternal(hdc);
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Graphics FromHdcInternal(IntPtr hdc)
    {
        // When a rendering backend (e.g. Impeller) is active, the HDC may be a synthetic
        // identifier allocated by the platform's GDI interop layer (ImpellerGdiInterop).
        // GDI+'s GdipCreateFromHDC would fail on these synthetic handles.
        //
        // Following the wxWidgets wxPaintDC pattern: the DC abstraction hides the
        // platform-specific surface. We create a stub GDI+ Graphics from a tiny bitmap
        // and attach the rendering backend which handles all actual drawing.
        if (BackendFactory is not null)
        {
            IRenderingBackend? backend = BackendFactory(IntPtr.Zero);
            if (backend is not null)
            {
                return CreateBackendBacked(backend);
            }

            return CreateOffscreenFallback();
        }

        return CreateOffscreenFallback();
    }

    /// <summary>
    ///  Creates a Graphics object backed purely by a rendering backend (no real GDI+ DC).
    ///  The GDI+ native graphics is a stub from a 1×1 bitmap — all actual drawing is
    ///  routed through <see cref="_backend"/>.
    /// </summary>
    private static Graphics CreateBackendBacked(IRenderingBackend backend)
    {
        return new Graphics(backend);
    }

    private static Graphics CreateOffscreenFallback() =>
        CreateBackendBacked(new ManagedBitmapRenderingBackend(new Bitmap(1, 1)));

    /// <summary>
    ///  Creates a new instance of the Graphics class from the specified handle to a device context and handle to a device.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Graphics FromHdc(IntPtr hdc, IntPtr hdevice)
    {
        if (BackendFactory is not null)
        {
            IRenderingBackend? backend = BackendFactory(IntPtr.Zero);
            if (backend is not null)
            {
                return CreateBackendBacked(backend);
            }

            return CreateOffscreenFallback();
        }

        return CreateOffscreenFallback();
    }

    /// <summary>
    ///  Creates a new instance of the <see cref='Graphics'/> class from a window handle.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Graphics FromHwnd(IntPtr hwnd) => FromHwndInternal(hwnd);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static Graphics FromHwndInternal(IntPtr hwnd)
    {
        // When a rendering backend is active, the HWND may be synthetic.
        // Bypass GDI+ and create a backend-backed Graphics.
        if (BackendFactory is not null)
        {
            IRenderingBackend? backend = BackendFactory(hwnd);
            if (backend is not null)
            {
                return CreateBackendBacked(backend);
            }

            return CreateOffscreenFallback();
        }

        return CreateOffscreenFallback();
    }

    private static void ThrowImpellerBackendUnavailable(string api)
    {
        throw new InvalidOperationException(
            $"{api} cannot use native GDI/GDI+. WinFormsX is Impeller-only; " +
            "the Impeller backend was not available for this drawing request.");
    }

    /// <summary>
    ///  Creates an instance of the <see cref='Graphics'/> class from an existing <see cref='Image'/>.
    /// </summary>
    public static Graphics FromImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if ((image.PixelFormat & PixelFormat.Indexed) != 0)
            throw new ArgumentException(SR.GdiplusCannotCreateGraphicsFromIndexedPixelFormat, nameof(image));

        if (image is Bitmap bitmap)
        {
            Graphics graphics = CreateBackendBacked(new ManagedBitmapRenderingBackend(bitmap));
            graphics._backingImage = image;
            return graphics;
        }

        if (BackendFactory is not null)
        {
            IRenderingBackend? backend = BackendFactory(IntPtr.Zero);
            if (backend is not null)
            {
                Graphics graphics = CreateBackendBacked(backend);
                graphics._backingImage = image;
                return graphics;
            }
        }

        ThrowImpellerBackendUnavailable(nameof(FromImage));
        throw null!;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void ReleaseHdcInternal(IntPtr hdc)
    {
        _nativeHdc = HDC.Null;
    }

    /// <summary>
    ///  Deletes this <see cref='Graphics'/>, and frees the memory allocated for it.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            while (_previousContext is not null)
            {
                // Dispose entire stack.
                GraphicsContext? context = _previousContext.Previous;
                _previousContext.Dispose();
                _previousContext = context;
            }

            if (PrintingHelper is HdcHandle printerDC)
            {
                printerDC.Dispose();
                _printingHelper = null;
            }
        }

        if (!_nativeHdc.IsNull)
        {
            ReleaseHdc();
        }

        NativeGraphics = null;
        _disposed = true;
    }

    ~Graphics() => Dispose(disposing: false);

    /// <summary>
    ///  Handle to native GDI+ graphics object. This object is created on demand.
    /// </summary>
    internal GpGraphics* NativeGraphics { get; private set; }

    internal bool IsDisposed => _disposed;

    internal bool IsHdcBusy => !_nativeHdc.IsNull;

    nint IPointer<GpGraphics>.Pointer => (nint)NativeGraphics;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public bool BeginFrame() => _backend?.BeginFrame(0, 0) ?? false;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public bool BeginFrame(int width, int height) => _backend?.BeginFrame(width, height) ?? false;

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void EndFrame() => _backend?.EndFrame(0, 0);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void EndFrame(int width, int height) => _backend?.EndFrame(width, height);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void AbortFrame() => _backend?.AbortFrame();

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void FlushPending() => _backend?.FlushPending();

    public Region Clip
    {
        get
        {
            return _clip?.Clone() ?? new Region(_clipBounds);
        }
        set => SetClip(value, Drawing2D.CombineMode.Replace);
    }

    public RectangleF ClipBounds => _clipBounds;

    /// <summary>
    /// Gets or sets the <see cref='Drawing2D.CompositingMode'/> associated with this <see cref='Graphics'/>.
    /// </summary>
    public Drawing2D.CompositingMode CompositingMode
    {
        get => _compositingMode;
        set
        {
            if (value is < Drawing2D.CompositingMode.SourceOver or > Drawing2D.CompositingMode.SourceCopy)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Drawing2D.CompositingMode));

            _compositingMode = value;
        }
    }

    public Drawing2D.CompositingQuality CompositingQuality
    {
        get => _compositingQuality;
        set
        {
            if (value is < Drawing2D.CompositingQuality.Invalid or > Drawing2D.CompositingQuality.AssumeLinear)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Drawing2D.CompositingQuality));

            _compositingQuality = value;
        }
    }

    public float DpiX => 96f;

    public float DpiY => 96f;

    /// <summary>
    /// Gets or sets the interpolation mode associated with this Graphics.
    /// </summary>
    public Drawing2D.InterpolationMode InterpolationMode
    {
        get => _interpolationMode;
        set
        {
            if (value is < Drawing2D.InterpolationMode.Invalid or > Drawing2D.InterpolationMode.HighQualityBicubic)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Drawing2D.InterpolationMode));

            _interpolationMode = value;
        }
    }

    public bool IsClipEmpty => _clipBounds.Width <= 0 || _clipBounds.Height <= 0;

    public bool IsVisibleClipEmpty => IsClipEmpty;

    public float PageScale
    {
        get => _pageScale;
        set => _pageScale = value;
    }

    public GraphicsUnit PageUnit
    {
        get => _pageUnit;
        set
        {
            if (value is < GraphicsUnit.World or > GraphicsUnit.Millimeter)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(GraphicsUnit));

            _pageUnit = value;
        }
    }

    public PixelOffsetMode PixelOffsetMode
    {
        get => _pixelOffsetMode;
        set
        {
            if (value is < PixelOffsetMode.Invalid or > PixelOffsetMode.Half)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(PixelOffsetMode));

            _pixelOffsetMode = value;
        }
    }

    public Point RenderingOrigin
    {
        get => _renderingOrigin;
        set => _renderingOrigin = value;
    }

    public Drawing2D.SmoothingMode SmoothingMode
    {
        get => _smoothingMode;
        set
        {
            if (value is < Drawing2D.SmoothingMode.Invalid or > Drawing2D.SmoothingMode.AntiAlias)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Drawing2D.SmoothingMode));

            _smoothingMode = value;
        }
    }

    public int TextContrast
    {
        get => _textContrast;
        set => _textContrast = value;
    }

    /// <summary>
    ///  Gets or sets the rendering mode for text associated with this <see cref='Graphics'/>.
    /// </summary>
    public TextRenderingHint TextRenderingHint
    {
        get => _textRenderingHint;
        set
        {
            if (value is < TextRenderingHint.SystemDefault or > TextRenderingHint.ClearTypeGridFit)
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(TextRenderingHint));

            _textRenderingHint = value;
        }
    }

    /// <summary>
    ///  Gets or sets the world transform for this <see cref='Graphics'/>.
    /// </summary>
    public Matrix Transform
    {
        get => _transform.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _transform = value.Clone();
            _transformElements = value.Elements.AsSpan() switch
            {
                [float m11, float m12, float m21, float m22, float dx, float dy] => new Matrix3x2(m11, m12, m21, m22, dx, dy),
                _ => Matrix3x2.Identity,
            };
        }
    }

    /// <summary>
    ///  Gets or sets the world transform elements for this <see cref="Graphics"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is a more performant alternative to <see cref="Transform"/> that does not need disposal.
    ///  </para>
    /// </remarks>
    public Matrix3x2 TransformElements
    {
        get => _transformElements;
        set
        {
            _transformElements = value;
            _transform = new Matrix(value.M11, value.M12, value.M21, value.M22, value.M31, value.M32);
        }
    }

    HDC IHdcContext.GetHdc() => (HDC)GetHdc();

    public IntPtr GetHdc()
    {
        if (_disposed)
        {
            throw new ArgumentException(null, nameof(GetHdc));
        }

        _nativeHdc = new HDC(unchecked((nint)0x1DCC1DCC));
        return _nativeHdc;
    }

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void ReleaseHdc(IntPtr hdc) => ReleaseHdcInternal(hdc);

    public void ReleaseHdc() => ReleaseHdcInternal(_nativeHdc);

    /// <summary>
    ///  Forces immediate execution of all operations currently on the stack.
    /// </summary>
    public void Flush() => Flush(Drawing2D.FlushIntention.Flush);

    /// <summary>
    ///  Forces execution of all operations currently on the stack.
    /// </summary>
    public void Flush(Drawing2D.FlushIntention intention)
    {
    }

    public void SetClip(Graphics g) => SetClip(g, Drawing2D.CombineMode.Replace);

    public void SetClip(Graphics g, Drawing2D.CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(g);
        _clipBounds = g.ClipBounds;
        _clip = g.Clip;
    }

    public void SetClip(Rectangle rect) => SetClip(rect, Drawing2D.CombineMode.Replace);

    public void SetClip(Rectangle rect, Drawing2D.CombineMode combineMode) => SetClip((RectangleF)rect, combineMode);

    public void SetClip(RectangleF rect) => SetClip(rect, Drawing2D.CombineMode.Replace);

    public void SetClip(RectangleF rect, Drawing2D.CombineMode combineMode)
    {
        if (combineMode == Drawing2D.CombineMode.Intersect || combineMode == Drawing2D.CombineMode.Replace)
        {
            _backend?.ClipRect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        if (combineMode == Drawing2D.CombineMode.Intersect)
        {
            _clipBounds = RectangleF.Intersect(_clipBounds, rect);
        }
        else if (combineMode == Drawing2D.CombineMode.Replace)
        {
            _clipBounds = rect;
        }
    }

    public void SetClip(GraphicsPath path) => SetClip(path, Drawing2D.CombineMode.Replace);

    public void SetClip(GraphicsPath path, Drawing2D.CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(path);
        throw GdiPlusRemoved(nameof(SetClip));
    }

    public void SetClip(Region region, Drawing2D.CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(region);
        _clip = region.Clone();
    }

    public void IntersectClip(Rectangle rect) => IntersectClip((RectangleF)rect);

    public void IntersectClip(RectangleF rect)
    {
        _backend?.ClipRect(rect.X, rect.Y, rect.Width, rect.Height);
        _clipBounds = RectangleF.Intersect(_clipBounds, rect);
    }

    public void IntersectClip(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        _clip = region.Clone();
    }

    public void ExcludeClip(Rectangle rect) =>
        throw GdiPlusRemoved(nameof(ExcludeClip));

    public void ExcludeClip(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        throw GdiPlusRemoved(nameof(ExcludeClip));
    }

    public void ResetClip() => _clipBounds = new RectangleF(0, 0, int.MaxValue, int.MaxValue);

    public void TranslateClip(float dx, float dy) => _clipBounds.Offset(dx, dy);

    public void TranslateClip(int dx, int dy) => TranslateClip((float)dx, dy);

    public bool IsVisible(int x, int y) => IsVisible((float)x, y);

    public bool IsVisible(Point point) => IsVisible(point.X, point.Y);

    public bool IsVisible(float x, float y)
    {
        return _clipBounds.Contains(x, y);
    }

    public bool IsVisible(PointF point) => IsVisible(point.X, point.Y);

    public bool IsVisible(int x, int y, int width, int height) => IsVisible((float)x, y, width, height);

    public bool IsVisible(Rectangle rect) => IsVisible((float)rect.X, rect.Y, rect.Width, rect.Height);

    public bool IsVisible(float x, float y, float width, float height)
    {
        return _clipBounds.IntersectsWith(new RectangleF(x, y, width, height));
    }

    public bool IsVisible(RectangleF rect) => IsVisible(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Resets the world transform to identity.
    /// </summary>
    public void ResetTransform()
    {
        _transform.Reset();
        _transformElements = Matrix3x2.Identity;
        _backend?.ResetTransform();
    }

    /// <summary>
    ///  Multiplies the <see cref='Matrix'/> that represents the world transform and <paramref name="matrix"/>.
    /// </summary>
    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    /// <summary>
    ///  Multiplies the <see cref='Matrix'/> that represents the world transform and <paramref name="matrix"/>.
    /// </summary>
    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        _transform.Multiply(matrix, order);
    }

    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    public void TranslateTransform(float dx, float dy, MatrixOrder order)
    {
        _backend?.Translate(dx, dy);
        _transform.Translate(dx, dy, order);
        _transformElements = Matrix3x2.Multiply(_transformElements, Matrix3x2.CreateTranslation(dx, dy));
    }

    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    public void ScaleTransform(float sx, float sy, MatrixOrder order)
    {
        _backend?.Scale(sx, sy);
        _transform.Scale(sx, sy, order);
        _transformElements = Matrix3x2.Multiply(_transformElements, Matrix3x2.CreateScale(sx, sy));
    }

    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    public void RotateTransform(float angle, MatrixOrder order)
    {
        _backend?.Rotate(angle * MathF.PI / 180f);
        _transform.Rotate(angle, order);
        _transformElements = Matrix3x2.Multiply(_transformElements, Matrix3x2.CreateRotation(angle * MathF.PI / 180f));
    }

    /// <summary>
    ///  Draws an arc from the specified ellipse.
    /// </summary>
    public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(pen);
        throw GdiPlusRemoved(nameof(DrawArc));
    }

    /// <summary>
    ///  Draws an arc from the specified ellipse.
    /// </summary>
    public void DrawArc(Pen pen, RectangleF rect, float startAngle, float sweepAngle) =>
        DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    /// <summary>
    /// Draws an arc from the specified ellipse.
    /// </summary>
    public void DrawArc(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle)
        => DrawArc(pen, (float)x, y, width, height, startAngle, sweepAngle);

    /// <summary>
    ///  Draws an arc from the specified ellipse.
    /// </summary>
    public void DrawArc(Pen pen, Rectangle rect, float startAngle, float sweepAngle) =>
        DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    /// <summary>
    ///  Draws a cubic Bezier curve defined by four ordered pairs that represent points.
    /// </summary>
    public void DrawBezier(Pen pen, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4)
    {
        ArgumentNullException.ThrowIfNull(pen);
        _backend?.StrokeLine(x1, y1, x4, y4, pen.Color, pen.Width);
    }

    /// <summary>
    ///  Draws a cubic Bezier curve defined by four points.
    /// </summary>
    public void DrawBezier(Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4) =>
        DrawBezier(pen, pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    /// <summary>
    ///  Draws a cubic Bezier curve defined by four points.
    /// </summary>
    public void DrawBezier(Pen pen, Point pt1, Point pt2, Point pt3, Point pt4) =>
        DrawBezier(pen, pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);

    /// <summary>
    ///  Draws the outline of a rectangle specified by <paramref name="rect"/>.
    /// </summary>
    /// <param name="pen">A Pen that determines the color, width, and style of the rectangle.</param>
    /// <param name="rect">A Rectangle structure that represents the rectangle to draw.</param>
    public void DrawRectangle(Pen pen, RectangleF rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Draws the outline of a rectangle specified by <paramref name="rect"/>.
    /// </summary>
    public void DrawRectangle(Pen pen, Rectangle rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="DrawRoundedRectangle(Pen, RectangleF, SizeF)"/>
    public void DrawRoundedRectangle(Pen pen, Rectangle rect, Size radius) =>
        DrawRoundedRectangle(pen, (RectangleF)rect, radius);

    /// <summary>
    ///  Draws the outline of the specified rounded rectangle.
    /// </summary>
    /// <param name="pen">The <see cref="Pen"/> to draw the outline with.</param>
    /// <param name="rect">The bounds of the rounded rectangle.</param>
    /// <param name="radius">The radius width and height used to round the corners of the rectangle.</param>
    public void DrawRoundedRectangle(Pen pen, RectangleF rect, SizeF radius)
    {
        using GraphicsPath path = new();
        path.AddRoundedRectangle(rect, radius);
        DrawPath(pen, path);
    }
#endif

    /// <summary>
    ///  Draws the outline of the specified rectangle.
    /// </summary>
    public void DrawRectangle(Pen pen, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(pen);
        
        if (_backend is not null)
        {
            _backend.StrokeRect(x, y, width, height, pen.Color, pen.Width);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawRectangle));
    }

    /// <summary>
    ///  Draws the outline of the specified rectangle.
    /// </summary>
    public void DrawRectangle(Pen pen, int x, int y, int width, int height)
        => DrawRectangle(pen, (float)x, y, width, height);

    /// <inheritdoc cref="DrawRectangles(Pen, Rectangle[])"/>
    public void DrawRectangles(Pen pen, params RectangleF[] rects) => DrawRectangles(pen, rects.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawRectangles(Pen, Rectangle[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawRectangles(Pen pen, params ReadOnlySpan<RectangleF> rects)
    {
        ArgumentNullException.ThrowIfNull(pen);

        foreach (RectangleF rect in rects)
        {
            _backend?.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, pen.Color, pen.Width);
        }
    }

    /// <summary>
    ///  Draws the outlines of a series of rectangles.
    /// </summary>
    /// <param name="pen"><see cref="Pen"/> that determines the color, width, and style of the outlines of the rectangles.</param>
    /// <param name="rects">An array of <see cref="Rectangle"/> structures that represents the rectangles to draw.</param>
    public void DrawRectangles(Pen pen, params Rectangle[] rects) => DrawRectangles(pen, rects.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawRectangles(Pen, Rectangle[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawRectangles(Pen pen, params ReadOnlySpan<Rectangle> rects)
    {
        ArgumentNullException.ThrowIfNull(pen);

        foreach (Rectangle rect in rects)
        {
            _backend?.StrokeRect(rect.X, rect.Y, rect.Width, rect.Height, pen.Color, pen.Width);
        }
    }

    /// <summary>
    ///  Draws the outline of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(Pen pen, RectangleF rect) => DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Draws the outline of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(Pen pen, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(pen);
        
        if (_backend is not null)
        {
            _backend.StrokeEllipse(x, y, width, height, pen.Color, pen.Width);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawEllipse));
    }

    /// <summary>
    ///  Draws the outline of an ellipse specified by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(Pen pen, Rectangle rect) => DrawEllipse(pen, (float)rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Draws the outline of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(Pen pen, int x, int y, int width, int height) => DrawEllipse(pen, (float)x, y, width, height);

    /// <summary>
    ///  Draws the outline of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void DrawPie(Pen pen, RectangleF rect, float startAngle, float sweepAngle) =>
        DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    /// <summary>
    ///  Draws the outline of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void DrawPie(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(pen);
        throw GdiPlusRemoved(nameof(DrawPie));
    }

    /// <summary>
    ///  Draws the outline of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void DrawPie(Pen pen, Rectangle rect, float startAngle, float sweepAngle) =>
        DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    /// <summary>
    ///  Draws the outline of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void DrawPie(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) =>
        DrawPie(pen, (float)x, y, width, height, startAngle, sweepAngle);

    /// <inheritdoc cref="DrawPolygon(Pen, Point[])"/>
    public void DrawPolygon(Pen pen, params PointF[] points) => DrawPolygon(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawPolygon(Pen, Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawPolygon(Pen pen, params ReadOnlySpan<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        for (int i = 1; i < points.Length; i++)
        {
            _backend?.StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, pen.Color, pen.Width);
        }

        if (points.Length > 2)
        {
            _backend?.StrokeLine(points[^1].X, points[^1].Y, points[0].X, points[0].Y, pen.Color, pen.Width);
        }
    }

    /// <summary>
    ///  Draws the outline of a polygon defined by an array of points.
    /// </summary>
    /// <param name="pen">The <see cref="Pen"/> to draw the outline with.</param>
    /// <param name="points">An array of <see cref="Point"/> structures that represent the vertices of the polygon.</param>
    public void DrawPolygon(Pen pen, params Point[] points) => DrawPolygon(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawPolygon(Pen, Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawPolygon(Pen pen, params ReadOnlySpan<Point> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        for (int i = 1; i < points.Length; i++)
        {
            _backend?.StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, pen.Color, pen.Width);
        }

        if (points.Length > 2)
        {
            _backend?.StrokeLine(points[^1].X, points[^1].Y, points[0].X, points[0].Y, pen.Color, pen.Width);
        }
    }

    /// <summary>
    ///  Draws the lines and curves defined by a <see cref='GraphicsPath'/>.
    /// </summary>
    public void DrawPath(Pen pen, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(path);
        throw GdiPlusRemoved(nameof(DrawPath));
    }

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, params PointF[] points) => DrawCurve(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawCurve(Pen pen, params ReadOnlySpan<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        for (int i = 1; i < points.Length; i++)
        {
            _backend?.StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, pen.Color, pen.Width);
        }
    }

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, PointF[] points, float tension) =>
        DrawCurve(pen, points.OrThrowIfNull().AsSpan(), tension);

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawCurve(Pen pen, ReadOnlySpan<PointF> points, float tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        DrawCurve(pen, points);
    }

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments) =>
        DrawCurve(pen, points, offset, numberOfSegments, 0.5f);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, ReadOnlySpan<PointF> points, int offset, int numberOfSegments) =>
        DrawCurve(pen, points, offset, numberOfSegments, 0.5f);
#endif

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments, float tension) =>
        DrawCurve(pen, points.OrThrowIfNull().AsSpan(), offset, numberOfSegments, tension);

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawCurve(Pen pen, ReadOnlySpan<PointF> points, int offset, int numberOfSegments, float tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ReadOnlySpan<PointF> segment = points.Slice(offset, Math.Min(numberOfSegments + 1, points.Length - offset));
        DrawCurve(pen, segment);
    }

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, params Point[] points) => DrawCurve(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawCurve(Pen pen, params ReadOnlySpan<Point> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        for (int i = 1; i < points.Length; i++)
        {
            _backend?.StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, pen.Color, pen.Width);
        }
    }

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
    public void DrawCurve(Pen pen, Point[] points, float tension) =>
        DrawCurve(pen, points.OrThrowIfNull().AsSpan(), tension);

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawCurve(Pen pen, ReadOnlySpan<Point> points, float tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        DrawCurve(pen, points);
    }

    /// <summary>
    ///  Draws a curve defined by an array of points.
    /// </summary>
    /// <param name="pen">The <see cref="Pen"/> to draw the curve with.</param>
    /// <param name="points">An array of points that define the curve.</param>
    /// <param name="offset">The index of the first point in the array to draw.</param>
    /// <param name="numberOfSegments">The number of segments to draw.</param>
    /// <param name="tension">A value greater than, or equal to zero that specifies the tension of the curve.</param>
    public void DrawCurve(Pen pen, Point[] points, int offset, int numberOfSegments, float tension) =>
        DrawCurve(pen, points.OrThrowIfNull().AsSpan(), offset, numberOfSegments, tension);

    /// <inheritdoc cref="DrawCurve(Pen, Point[], int, int, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawCurve(Pen pen, ReadOnlySpan<Point> points, int offset, int numberOfSegments, float tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ReadOnlySpan<Point> segment = points.Slice(offset, Math.Min(numberOfSegments + 1, points.Length - offset));
        DrawCurve(pen, segment);
    }

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>
    public void DrawClosedCurve(Pen pen, params PointF[] points) =>
        DrawClosedCurve(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawClosedCurve(Pen pen, params ReadOnlySpan<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        DrawPolygon(pen, points);
    }

    /// <summary>
    ///  Draws a closed curve defined by an array of points.
    /// </summary>
    /// <param name="pen">The <see cref="Pen"/> to draw the closed curve with.</param>
    /// <param name="points">An array of points that define the closed curve.</param>
    /// <param name="tension">A value greater than, or equal to zero that specifies the tension of the curve.</param>
    /// <param name="fillmode">A <see cref="FillMode"/> enumeration that specifies the fill mode of the curve.</param>
    public void DrawClosedCurve(Pen pen, PointF[] points, float tension, FillMode fillmode) =>
        DrawClosedCurve(pen, points.OrThrowIfNull().AsSpan(), tension, fillmode);

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawClosedCurve(Pen pen, ReadOnlySpan<PointF> points, float tension, FillMode fillmode)
    {
        ArgumentNullException.ThrowIfNull(pen);
        DrawPolygon(pen, points);
    }

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>
    public void DrawClosedCurve(Pen pen, params Point[] points) => DrawClosedCurve(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawClosedCurve(Pen pen, params ReadOnlySpan<Point> points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        DrawPolygon(pen, points);
    }

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>

    public void DrawClosedCurve(Pen pen, Point[] points, float tension, FillMode fillmode) =>
        DrawClosedCurve(pen, points.OrThrowIfNull().AsSpan(), tension, fillmode);

    /// <inheritdoc cref="DrawClosedCurve(Pen, PointF[], float, FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawClosedCurve(Pen pen, ReadOnlySpan<Point> points, float tension, FillMode fillmode)
    {
        ArgumentNullException.ThrowIfNull(pen);
        DrawPolygon(pen, points);
    }

    /// <summary>
    ///  Fills the entire drawing surface with the specified color.
    /// </summary>
    public void Clear(Color color)
    {
        if (_backend is not null)
        {
            _backend.Clear(color);
            return;
        }

        throw GdiPlusRemoved(nameof(Clear));
    }

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="FillRoundedRectangle(Brush, RectangleF, SizeF)"/>/>
    public void FillRoundedRectangle(Brush brush, Rectangle rect, Size radius) =>
        FillRoundedRectangle(brush, (RectangleF)rect, radius);

    /// <summary>
    ///  Fills the interior of a rounded rectangle with a <see cref='Brush'/>.
    /// </summary>
    /// <param name="brush">The <see cref="Brush"/> to fill the rounded rectangle with.</param>
    /// <param name="rect">The bounds of the rounded rectangle.</param>
    /// <param name="radius">The radius width and height used to round the corners of the rectangle.</param>
    public void FillRoundedRectangle(Brush brush, RectangleF rect, SizeF radius)
    {
        using GraphicsPath path = new();
        path.AddRoundedRectangle(rect, radius);
        FillPath(brush, path);
    }
#endif

    /// <summary>
    ///  Fills the interior of a rectangle with a <see cref='Brush'/>.
    /// </summary>
    public void FillRectangle(Brush brush, RectangleF rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Fills the interior of a rectangle with a <see cref='Brush'/>.
    /// </summary>
    public void FillRectangle(Brush brush, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(brush);

        if (_backend is not null)
        {
            Color color = BrushColor(brush);
            if (color != Color.Transparent)
            {
                _backend.FillRect(x, y, width, height, color);
            }

            return;
        }

        throw GdiPlusRemoved(nameof(FillRectangle));
    }

    /// <summary>
    ///  Fills the interior of a rectangle with a <see cref='Brush'/>.
    /// </summary>
    public void FillRectangle(Brush brush, Rectangle rect) => FillRectangle(brush, (float)rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Fills the interior of a rectangle with a <see cref='Brush'/>.
    /// </summary>
    public void FillRectangle(Brush brush, int x, int y, int width, int height) => FillRectangle(brush, (float)x, y, width, height);

    /// <summary>
    ///  Fills the interiors of a series of rectangles with a <see cref='Brush'/>.
    /// </summary>
    /// <param name="brush">The <see cref="Brush"/> to fill the rectangles with.</param>
    /// <param name="rects">An array of rectangles to fill.</param>
    public void FillRectangles(Brush brush, params RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        FillRectangles(brush, rects.AsSpan());
    }

    /// <inheritdoc cref="FillRectangles(Brush, RectangleF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillRectangles(Brush brush, params ReadOnlySpan<RectangleF> rects)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var backend = _backend;
        if (backend is object && brush is SolidBrush solidBrush)
        {
            foreach (var rect in rects)
            {
                backend.FillRect(rect.X, rect.Y, rect.Width, rect.Height, solidBrush.Color);
            }

            return;
        }

        throw GdiPlusRemoved(nameof(FillRectangles));
    }

    /// <inheritdoc cref="FillRectangles(Brush, RectangleF[])"/>
    public void FillRectangles(Brush brush, params Rectangle[] rects) =>
        FillRectangles(brush, rects.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="FillRectangles(Brush, RectangleF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillRectangles(Brush brush, params ReadOnlySpan<Rectangle> rects)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var backend = _backend;
        if (backend is object && brush is SolidBrush solidBrush)
        {
            foreach (var rect in rects)
            {
                backend.FillRect(rect.X, rect.Y, rect.Width, rect.Height, solidBrush.Color);
            }

            return;
        }

        throw GdiPlusRemoved(nameof(FillRectangles));
    }

    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
    public void FillPolygon(Brush brush, params PointF[] points) => FillPolygon(brush, points, FillMode.Alternate);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
    public void FillPolygon(Brush brush, params ReadOnlySpan<PointF> points) => FillPolygon(brush, points, FillMode.Alternate);
#endif

    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
    public void FillPolygon(Brush brush, PointF[] points, FillMode fillMode) =>
        FillPolygon(brush, points.OrThrowIfNull().AsSpan(), fillMode);

    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillPolygon(Brush brush, ReadOnlySpan<PointF> points, FillMode fillMode)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillPolygon));
    }

    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
    public void FillPolygon(Brush brush, Point[] points) => FillPolygon(brush, points, FillMode.Alternate);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
    public void FillPolygon(Brush brush, params ReadOnlySpan<Point> points) => FillPolygon(brush, points, FillMode.Alternate);
#endif

    /// <summary>
    ///  Fills the interior of a polygon defined by an array of points.
    /// </summary>
    /// <param name="brush">The <see cref="Brush"/> to fill the polygon with.</param>
    /// <param name="points">An array points that represent the vertices of the polygon.</param>
    /// <param name="fillMode">A <see cref="FillMode"/> enumeration that specifies the fill mode of the polygon.</param>
    public void FillPolygon(Brush brush, Point[] points, FillMode fillMode) =>
        FillPolygon(brush, points.OrThrowIfNull().AsSpan(), fillMode);

    /// <inheritdoc cref="FillPolygon(Brush, Point[], FillMode)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillPolygon(Brush brush, ReadOnlySpan<Point> points, FillMode fillMode)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillPolygon));
    }

    /// <summary>
    ///  Fills the interior of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void FillEllipse(Brush brush, RectangleF rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Fills the interior of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void FillEllipse(Brush brush, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(brush);

        if (_backend is not null)
        {
            _backend.FillEllipse(x, y, width, height, BrushColor(brush));
            return;
        }

        throw GdiPlusRemoved(nameof(FillEllipse));
    }

    /// <summary>
    ///  Fills the interior of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void FillEllipse(Brush brush, Rectangle rect) => FillEllipse(brush, (float)rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>
    ///  Fills the interior of an ellipse defined by a bounding rectangle.
    /// </summary>
    public void FillEllipse(Brush brush, int x, int y, int width, int height) => FillEllipse(brush, (float)x, y, width, height);

    /// <summary>
    ///  Fills the interior of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void FillPie(Brush brush, Rectangle rect, float startAngle, float sweepAngle) =>
        FillPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    /// <summary>
    ///  Fills the interior of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    /// <param name="brush">A Brush that determines the characteristics of the fill.</param>
    /// <param name="rect">A Rectangle structure that represents the bounding rectangle that defines the ellipse from which the pie section comes.</param>
    /// <param name="startAngle">Angle in degrees measured clockwise from the x-axis to the first side of the pie section.</param>
    /// <param name="sweepAngle">Angle in degrees measured clockwise from the <paramref name="startAngle"/> parameter to the second side of the pie section.</param>
    public void FillPie(Brush brush, RectangleF rect, float startAngle, float sweepAngle) =>
        FillPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    /// <summary>
    ///  Fills the interior of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillPie));
    }

    /// <summary>
    ///  Fills the interior of a pie section defined by an ellipse and two radial lines.
    /// </summary>
    public void FillPie(Brush brush, int x, int y, int width, int height, int startAngle, int sweepAngle)
        => FillPie(brush, (float)x, y, width, height, startAngle, sweepAngle);

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, params PointF[] points) =>
        FillClosedCurve(brush, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillClosedCurve(Brush brush, params ReadOnlySpan<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillClosedCurve));
    }

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, PointF[] points, FillMode fillmode) =>
        FillClosedCurve(brush, points, fillmode, 0.5f);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, ReadOnlySpan<PointF> points, FillMode fillmode) =>
        FillClosedCurve(brush, points, fillmode, 0.5f);
#endif

    /// <summary>
    ///  Fills the interior of a closed curve defined by an array of points.
    /// </summary>
    /// <param name="brush">The <see cref="Brush"/> to fill the closed curve with.</param>
    /// <param name="points">An array of points that make up the closed curve.</param>
    /// <param name="fillmode">A <see cref="FillMode"/> enumeration that specifies the fill mode of the closed curve.</param>
    /// <param name="tension">A value greater than, or equal to zero that specifies the tension of the curve.</param>
    public void FillClosedCurve(Brush brush, PointF[] points, FillMode fillmode, float tension) =>
        FillClosedCurve(brush, points.OrThrowIfNull().AsSpan(), fillmode, tension);

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillClosedCurve(Brush brush, ReadOnlySpan<PointF> points, FillMode fillmode, float tension)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillClosedCurve));
    }

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, params Point[] points) =>
        FillClosedCurve(brush, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillClosedCurve(Brush brush, params ReadOnlySpan<Point> points)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillClosedCurve));
    }

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, Point[] points, FillMode fillmode) =>
        FillClosedCurve(brush, points, fillmode, 0.5f);

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, ReadOnlySpan<Point> points, FillMode fillmode) =>
        FillClosedCurve(brush, points, fillmode, 0.5f);

#endif

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
    public void FillClosedCurve(Brush brush, Point[] points, FillMode fillmode, float tension) =>
        FillClosedCurve(brush, points.OrThrowIfNull().AsSpan(), fillmode, tension);

    /// <inheritdoc cref="FillClosedCurve(Brush, PointF[], FillMode, float)"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void FillClosedCurve(Brush brush, ReadOnlySpan<Point> points, FillMode fillmode, float tension)
    {
        ArgumentNullException.ThrowIfNull(brush);

        throw GdiPlusRemoved(nameof(FillClosedCurve));
    }

    /// <summary>
    ///  Draws the specified text at the specified location with the specified <see cref="Brush"/> and
    ///  <see cref="Font"/> objects.
    /// </summary>
    /// <param name="s">The text to draw.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="brush"><see cref="Brush"/> that determines the color and texture of the drawn text.</param>
    /// <param name="x">The x-coordinate of the upper-left corner of the drawn text.</param>
    /// <param name="y">The y-coordinate of the upper-left corner of the drawn text.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="brush"/> is <see langword="null"/>. -or- <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public void DrawString(string? s, Font font, Brush brush, float x, float y) =>
        DrawString(s, font, brush, new RectangleF(x, y, 0, 0), null);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="DrawString(string?, Font, Brush, float, float)"/>
    public void DrawString(ReadOnlySpan<char> s, Font font, Brush brush, float x, float y) =>
        DrawString(s, font, brush, new RectangleF(x, y, 0, 0), null);
#endif

    /// <summary>
    ///  Draws the specified text at the specified location with the specified <see cref="Brush"/> and
    ///  <see cref="Font"/> objects.
    /// </summary>
    /// <param name="s">The text to draw.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="brush"><see cref="Brush"/> that determines the color and texture of the drawn text.</param>
    /// <param name="point"><see cref="PointF"/>structure that specifies the upper-left corner of the drawn text.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="brush"/> is <see langword="null"/>. -or- <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public void DrawString(string? s, Font font, Brush brush, PointF point) =>
        DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), null);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="DrawString(string?, Font, Brush, PointF)"/>
    public void DrawString(ReadOnlySpan<char> s, Font font, Brush brush, PointF point) =>
        DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), null);
#endif

    /// <summary>
    ///  Draws the specified text at the specified location with the specified <see cref="Brush"/> and
    ///  <see cref="Font"/> objects using the formatting attributes of the specified <see cref="StringFormat"/>.
    /// </summary>
    /// <param name="s">The text to draw.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="brush"><see cref="Brush"/> that determines the color and texture of the drawn text.</param>
    /// <param name="x">The x-coordinate of the upper-left corner of the drawn text.</param>
    /// <param name="y">The y-coordinate of the upper-left corner of the drawn text.</param>
    /// <param name="format">
    ///  <see cref="StringFormat"/> that specifies formatting attributes, such as line spacing and alignment,
    ///  that are applied to the drawn text.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="brush"/> is <see langword="null"/>. -or- <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public void DrawString(string? s, Font font, Brush brush, float x, float y, StringFormat? format) =>
        DrawString(s, font, brush, new RectangleF(x, y, 0, 0), format);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="DrawString(string?, Font, Brush, float, float, StringFormat?)"/>
    public void DrawString(ReadOnlySpan<char> s, Font font, Brush brush, float x, float y, StringFormat? format) =>
        DrawString(s, font, brush, new RectangleF(x, y, 0, 0), format);
#endif

    /// <summary>
    ///  Draws the specified text at the specified location with the specified <see cref="Brush"/> and
    ///  <see cref="Font"/> objects using the formatting attributes of the specified <see cref="StringFormat"/>.
    /// </summary>
    /// <param name="s">The text to draw.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="brush"><see cref="Brush"/> that determines the color and texture of the drawn text.</param>
    /// <param name="point"><see cref="PointF"/>structure that specifies the upper-left corner of the drawn text.</param>
    /// <param name="format">
    ///  <see cref="StringFormat"/> that specifies formatting attributes, such as line spacing and alignment,
    ///  that are applied to the drawn text.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="brush"/> is <see langword="null"/>. -or- <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    public void DrawString(string? s, Font font, Brush brush, PointF point, StringFormat? format) =>
        DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), format);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="DrawString(string?, Font, Brush, PointF, StringFormat?)"/>
    public void DrawString(ReadOnlySpan<char> s, Font font, Brush brush, PointF point, StringFormat? format) =>
        DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), format);
#endif

    /// <summary>
    ///  Draws the specified text in the specified rectangle with the specified <see cref="Brush"/> and
    ///  <see cref="Font"/> objects.
    /// </summary>
    /// <param name="s">The text to draw.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="brush"><see cref="Brush"/> that determines the color and texture of the drawn text.</param>
    /// <param name="layoutRectangle"><see cref="RectangleF"/>structure that specifies the location of the drawn text.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="brush"/> is <see langword="null"/>. -or- <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///  <para>
    ///   The text represented by the <paramref name="s"/> parameter is drawn inside the rectangle represented by
    ///   the <paramref name="layoutRectangle"/> parameter. If the text does not fit inside the rectangle, it is
    ///   truncated at the nearest word. To further manipulate how the string is drawn inside the rectangle use the
    ///   <see cref="DrawString(string?, Font, Brush, RectangleF, StringFormat?)"/> overload that takes
    ///   a <see cref="StringFormat"/>.
    ///  </para>
    /// </remarks>
    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle) =>
        DrawString(s, font, brush, layoutRectangle, null);

#if NET8_0_OR_GREATER
    /// <remarks>
    ///  <para>
    ///   The text represented by the <paramref name="s"/> parameter is drawn inside the rectangle represented by
    ///   the <paramref name="layoutRectangle"/> parameter. If the text does not fit inside the rectangle, it is
    ///   truncated at the nearest word. To further manipulate how the string is drawn inside the rectangle use the
    ///   <see cref="DrawString(ReadOnlySpan{char}, Font, Brush, RectangleF, StringFormat?)"/> overload that takes
    ///   a <see cref="StringFormat"/>.
    ///  </para>
    /// </remarks>
    /// <inheritdoc cref="DrawString(string?, Font, Brush, RectangleF)"/>
    public void DrawString(ReadOnlySpan<char> s, Font font, Brush brush, RectangleF layoutRectangle) =>
        DrawString(s, font, brush, layoutRectangle, null);
#endif

    /// <summary>
    ///  Draws the specified text in the specified rectangle with the specified <see cref="Brush"/> and
    ///  <see cref="Font"/> objects using the formatting attributes of the specified <see cref="StringFormat"/>.
    /// </summary>
    /// <param name="s">The text to draw.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="brush"><see cref="Brush"/> that determines the color and texture of the drawn text.</param>
    /// <param name="layoutRectangle"><see cref="RectangleF"/>structure that specifies the location of the drawn text.</param>
    /// <param name="format">
    ///  <see cref="StringFormat"/> that specifies formatting attributes, such as line spacing and alignment,
    ///  that are applied to the drawn text.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="brush"/> is <see langword="null"/>. -or- <paramref name="font"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///  <para>
    ///   The text represented by the <paramref name="s"/> parameter is drawn inside the rectangle represented by
    ///   the <paramref name="layoutRectangle"/> parameter. If the text does not fit inside the rectangle, it is
    ///   truncated at the nearest word, unless otherwise specified with the <paramref name="format"/> parameter.
    ///  </para>
    /// </remarks>
    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format) =>
        DrawStringInternal(s, font, brush, layoutRectangle, format);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="DrawString(string?, Font, Brush, RectangleF, StringFormat?)"/>
    public void DrawString(ReadOnlySpan<char> s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format) =>
        DrawStringInternal(s, font, brush, layoutRectangle, format);
#endif

    private void DrawStringInternal(ReadOnlySpan<char> s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(brush);

        if (s.IsEmpty)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(font);

        if (_backend is not null)
        {
            Color color = brush is SolidBrush sb ? sb.Color : Color.Black;
            bool bold = font.Style.HasFlag(FontStyle.Bold);
            bool italic = font.Style.HasFlag(FontStyle.Italic);
            float fontSizePixels = font.SizeInPoints * (this.DpiY / 72.0f);
            
            ContentAlignment alignment = ContentAlignment.TopLeft;
            if (format is not null)
            {
                // Map Horizontal Alignment
                if (format.Alignment == StringAlignment.Center) alignment = ContentAlignment.TopCenter;
                else if (format.Alignment == StringAlignment.Far) alignment = ContentAlignment.TopRight;

                // Map Vertical Alignment
                if (format.LineAlignment == StringAlignment.Center)
                {
                    if (alignment == ContentAlignment.TopLeft) alignment = ContentAlignment.MiddleLeft;
                    else if (alignment == ContentAlignment.TopCenter) alignment = ContentAlignment.MiddleCenter;
                    else if (alignment == ContentAlignment.TopRight) alignment = ContentAlignment.MiddleRight;
                }
                else if (format.LineAlignment == StringAlignment.Far)
                {
                    if (alignment == ContentAlignment.TopLeft) alignment = ContentAlignment.BottomLeft;
                    else if (alignment == ContentAlignment.TopCenter) alignment = ContentAlignment.BottomCenter;
                    else if (alignment == ContentAlignment.TopRight) alignment = ContentAlignment.BottomRight;
                }
            }

            _backend.DrawStringAligned(s.ToString(), layoutRectangle, alignment, color, font.FontFamily.Name, fontSizePixels, bold, italic);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawString));
    }

    /// <param name="charactersFitted">Number of characters in the text.</param>
    /// <param name="linesFilled">Number of lines in the text.</param>
    /// <inheritdoc cref="MeasureString(string?, Font, SizeF, StringFormat?)"/>
    public SizeF MeasureString(
        string? text,
        Font font,
        SizeF layoutArea,
        StringFormat? stringFormat,
        out int charactersFitted,
        out int linesFilled) =>
        MeasureStringInternal(text, font, new(default, layoutArea), stringFormat, out charactersFitted, out linesFilled);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font, SizeF, StringFormat?, out int, out int)"/>
    public SizeF MeasureString(
        ReadOnlySpan<char> text,
        Font font,
        SizeF layoutArea,
        StringFormat? stringFormat,
        out int charactersFitted,
        out int linesFilled) =>
        MeasureStringInternal(text, font, new(default, layoutArea), stringFormat, out charactersFitted, out linesFilled);
#endif

    public SizeF MeasureStringInternal(
        ReadOnlySpan<char> text,
        Font font,
        RectangleF layoutArea,
        StringFormat? stringFormat,
        out int charactersFitted,
        out int linesFilled)
    {
        if (text.IsEmpty)
        {
            charactersFitted = 0;
            linesFilled = 0;
            return SizeF.Empty;
        }

        ArgumentNullException.ThrowIfNull(font);

        if (_backend is not null)
        {
            bool bold = font.Style.HasFlag(FontStyle.Bold);
            bool italic = font.Style.HasFlag(FontStyle.Italic);
            float fontSizePixels = font.SizeInPoints * (this.DpiY / 72.0f);
            
            var size = _backend.MeasureString(text.ToString(), font.FontFamily.Name, fontSizePixels, bold, italic);
            charactersFitted = text.Length;
            linesFilled = 1;
            return size;
        }

        charactersFitted = text.Length;
        linesFilled = 1;
        float measuredFontSizePixels = font.SizeInPoints * (DpiY / 72.0f);
        return new SizeF(text.Length * measuredFontSizePixels * 0.55f, font.GetHeight(this));
    }

    /// <param name="origin"><see cref="PointF"/> structure that represents the upper-left corner of the text.</param>
    /// <inheritdoc cref="MeasureString(string?, Font, SizeF, StringFormat?)"/>
    public SizeF MeasureString(string? text, Font font, PointF origin, StringFormat? stringFormat)
        => MeasureStringInternal(text, font, new(origin, default), stringFormat, out _, out _);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font, PointF, StringFormat?)"/>
    public SizeF MeasureString(ReadOnlySpan<char> text, Font font, PointF origin, StringFormat? stringFormat)
        => MeasureStringInternal(text, font, new(origin, default), stringFormat, out _, out _);
#endif

    /// <inheritdoc cref="MeasureString(string?, Font, SizeF, StringFormat?)"/>
    public SizeF MeasureString(string? text, Font font, SizeF layoutArea) => MeasureString(text, font, layoutArea, null);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font, SizeF)"/>
    public SizeF MeasureString(ReadOnlySpan<char> text, Font font, SizeF layoutArea) => MeasureString(text, font, layoutArea, null);
#endif

    /// <param name="stringFormat"><see cref="StringFormat"/> that represents formatting information, such as line spacing, for the text.</param>
    /// <param name="layoutArea"><see cref="SizeF"/> structure that specifies the maximum layout area for the text.</param>
    /// <inheritdoc cref="MeasureString(string?, Font, int, StringFormat?)"/>
    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? stringFormat) =>
        MeasureStringInternal(text, font, new(default, layoutArea), stringFormat, out _, out _);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font, SizeF, StringFormat?)"/>
    public SizeF MeasureString(ReadOnlySpan<char> text, Font font, SizeF layoutArea, StringFormat? stringFormat) =>
        MeasureStringInternal(text, font, new(default, layoutArea), stringFormat, out _, out _);
#endif

    /// <summary>
    ///  Measures the specified text when drawn with the specified <see cref="Font"/>.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <returns>
    ///  This method returns a <see cref="SizeF"/> structure that represents the size, in the units specified by the
    ///  <see cref="PageUnit"/> property, of the text specified by the <paramref name="text"/> parameter as drawn
    ///  with the <paramref name="font"/> parameter.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   The <see cref="MeasureString(string?, Font)"/> method is designed for use with individual strings and
    ///   includes a small amount of extra space before and after the string to allow for overhanging glyphs. Also,
    ///   the <see cref="DrawString(string?, Font, Brush, PointF)"/> method adjusts glyph points to optimize display
    ///   quality and might display a string narrower than reported by <see cref="MeasureString(string?, Font)"/>.
    ///   To obtain metrics suitable for adjacent strings in layout (for example, when implementing formatted text),
    ///   use the <see cref="MeasureCharacterRanges(string?, Font, RectangleF, StringFormat?)"/> method or one of
    ///   the <see cref="MeasureString(string?, Font, int, StringFormat?)"/> methods that takes a StringFormat, and
    ///   pass <see cref="StringFormat.GenericTypographic"/>. Also, ensure the <see cref="TextRenderingHint"/> for
    ///   the <see cref="Graphics"/> is <see cref="TextRenderingHint.AntiAlias"/>.
    ///  </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is null.</exception>
    public SizeF MeasureString(string? text, Font font) => MeasureString(text, font, new SizeF(0, 0));

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font)"/>
    public SizeF MeasureString(ReadOnlySpan<char> text, Font font) => MeasureString(text, font, new SizeF(0, 0));
#endif

    /// <param name="width">Maximum width of the string in pixels.</param>
    /// <inheritdoc cref="MeasureString(string?, Font)"/>
    public SizeF MeasureString(string? text, Font font, int width) => MeasureString(text, font, new SizeF(width, 999999));

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font, int)"/>
    public SizeF MeasureString(ReadOnlySpan<char> text, Font font, int width) =>
        MeasureString(text, font, new SizeF(width, 999999));
#endif

    /// <param name="format"><see cref="StringFormat"/> that represents formatting information, such as line spacing, for the text.</param>
    /// <inheritdoc cref="MeasureString(string?, Font, int)"/>
    public SizeF MeasureString(string? text, Font font, int width, StringFormat? format) =>
        MeasureString(text, font, new SizeF(width, 999999), format);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureString(string?, Font, int, StringFormat?)"/>
    public SizeF MeasureString(ReadOnlySpan<char> text, Font font, int width, StringFormat? format) =>
        MeasureString(text, font, new SizeF(width, 999999), format);
#endif

    /// <summary>
    ///  Gets an array of <see cref="Region"/> objects, each of which bounds a range of character positions within
    ///  the specified text.
    /// </summary>
    /// <param name="text">Text to measure.</param>
    /// <param name="font"><see cref="Font"/> that defines the text format.</param>
    /// <param name="layoutRect"><see cref="RectangleF"/> structure that specifies the layout rectangle for the text.</param>
    /// <param name="stringFormat"><see cref="StringFormat"/> that represents formatting information, such as line spacing, for the text.</param>
    /// <returns>
    ///  This method returns an array of <see cref="Region"/> objects, each of which bounds a range of character
    ///  positions within the specified text.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   The regions returned by this method are resolution-dependent, so there might be a slight loss of accuracy
    ///   if text is recorded in a metafile at one resolution and later played back at a different resolution.
    ///  </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    public Region[] MeasureCharacterRanges(string? text, Font font, RectangleF layoutRect, StringFormat? stringFormat) =>
        MeasureCharacterRangesInternal(text, font, layoutRect, stringFormat);

#if NET8_0_OR_GREATER
    /// <inheritdoc cref="MeasureCharacterRanges(string?, Font, RectangleF, StringFormat?)"/>
    public Region[] MeasureCharacterRanges(ReadOnlySpan<char> text, Font font, RectangleF layoutRect, StringFormat? stringFormat) =>
        MeasureCharacterRangesInternal(text, font, layoutRect, stringFormat);
#endif

    private Region[] MeasureCharacterRangesInternal(
        ReadOnlySpan<char> text,
        Font font,
        RectF layoutRect,
        StringFormat? stringFormat)
    {
        if (text.IsEmpty)
            return [];

        ArgumentNullException.ThrowIfNull(font);

        return [new Region(layoutRect)];
    }

    /// <summary>
    ///  Draws the specified image at the specified location.
    /// </summary>
    public void DrawImage(Image image, PointF point) => DrawImage(image, point.X, point.Y);

    public void DrawImage(Image image, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImage(image, x, y, image.Width, image.Height);
    }

    public void DrawImage(Image image, RectangleF rect) => DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawImage(Image image, float x, float y, float width, float height)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_backend is not null)
        {
            _backend.DrawImageRect(image, 0, 0, image.Width, image.Height, x, y, width, height);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, Point point) => DrawImage(image, (float)point.X, point.Y);

    public void DrawImage(Image image, int x, int y) => DrawImage(image, (float)x, y);

    public void DrawImage(Image image, Rectangle rect) => DrawImage(image, (float)rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawImage(Image image, int x, int y, int width, int height) => DrawImage(image, (float)x, y, width, height);

    public void DrawImageUnscaled(Image image, Point point) => DrawImage(image, point.X, point.Y);

    public void DrawImageUnscaled(Image image, int x, int y) => DrawImage(image, x, y);

    public void DrawImageUnscaled(Image image, Rectangle rect) => DrawImage(image, rect.X, rect.Y);

    public void DrawImageUnscaled(Image image, int x, int y, int width, int height) => DrawImage(image, x, y);

    public void DrawImageUnscaledAndClipped(Image image, Rectangle rect)
    {
        ArgumentNullException.ThrowIfNull(image);

        int width = Math.Min(rect.Width, image.Width);
        int height = Math.Min(rect.Height, image.Height);

        // We could put centering logic here too for the case when the image
        // is smaller than the rect.
        DrawImage(image, rect, 0, 0, width, height, GraphicsUnit.Pixel);
    }

    public void DrawImage(Image image, PointF[] destPoints)
    {
        // Affine or perspective blt
        //
        //  destPoints.Length = 3: rect => parallelogram
        //      destPoints[0] <=> top-left corner of the source rectangle
        //      destPoints[1] <=> top-right corner
        //      destPoints[2] <=> bottom-left corner
        //  destPoints.Length = 4: rect => quad
        //      destPoints[3] <=> bottom-right corner
        //
        //  @notes Perspective blt only works for bitmap images.

        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);

        int count = destPoints.Length;
        if (count is not 3 and not 4)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidLength);

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, Point[] destPoints)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);

        int count = destPoints.Length;
        if (count is not 3 and not 4)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidLength);

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, float x, float y, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_backend is not null)
        {
            _backend.DrawImageRect(image, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, x, y, srcRect.Width, srcRect.Height);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, int x, int y, Rectangle srcRect, GraphicsUnit srcUnit)
        => DrawImage(image, x, y, (RectangleF)srcRect, srcUnit);

    public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (_backend is not null)
        {
            _backend.DrawImageRect(image, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, destRect.X, destRect.Y, destRect.Width, destRect.Height);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit)
        => DrawImage(image, destRect, srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, srcUnit);

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);

        int count = destPoints.Length;
        if (count is not 3 and not 4)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidLength);

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes? imageAttr) =>
        DrawImage(image, destPoints, srcRect, srcUnit, imageAttr, null, 0);

    public void DrawImage(
        Image image,
        PointF[] destPoints,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr,
        DrawImageAbort? callback) =>
        DrawImage(image, destPoints, srcRect, srcUnit, imageAttr, callback, 0);

    public void DrawImage(
        Image image,
        PointF[] destPoints,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr,
        DrawImageAbort? callback,
        int callbackData)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);

        int count = destPoints.Length;
        if (count is not 3 and not 4)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidLength);

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(Image image, Point[] destPoints, Rectangle srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, destPoints, srcRect, srcUnit, null, null, 0);

    public void DrawImage(
        Image image,
        Point[] destPoints,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr) => DrawImage(image, destPoints, srcRect, srcUnit, imageAttr, null, 0);

    public void DrawImage(
        Image image,
        Point[] destPoints,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr,
        DrawImageAbort? callback) => DrawImage(image, destPoints, srcRect, srcUnit, imageAttr, callback, 0);

    public void DrawImage(
        Image image,
        Point[] destPoints,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr,
        DrawImageAbort? callback,
        int callbackData)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);

        int count = destPoints.Length;
        if (count is not 3 and not 4)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidLength);

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(
        Image image,
        Rectangle destRect,
        float srcX,
        float srcY,
        float srcWidth,
        float srcHeight,
        GraphicsUnit srcUnit) => DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, null);

    public void DrawImage(
        Image image,
        Rectangle destRect,
        float srcX,
        float srcY,
        float srcWidth,
        float srcHeight,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttrs) => DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs, null);

    public void DrawImage(
        Image image,
        Rectangle destRect,
        float srcX,
        float srcY,
        float srcWidth,
        float srcHeight,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttrs,
        DrawImageAbort? callback) => DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs, callback, IntPtr.Zero);

    public void DrawImage(
        Image image,
        Rectangle destRect,
        float srcX,
        float srcY,
        float srcWidth,
        float srcHeight,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttrs,
        DrawImageAbort? callback,
        IntPtr callbackData)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (imageAttrs is not null || callback is not null)
        {
            throw GdiPlusRemoved(nameof(DrawImage));
        }

        if (_backend is not null)
        {
            _backend.DrawImageRect(image, srcX, srcY, srcWidth, srcHeight, destRect.X, destRect.Y, destRect.Width, destRect.Height);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawImage));
    }

    public void DrawImage(
        Image image,
        Rectangle destRect,
        int srcX,
        int srcY,
        int srcWidth,
        int srcHeight,
        GraphicsUnit srcUnit) => DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, null);

    public void DrawImage(
        Image image,
        Rectangle destRect,
        int srcX,
        int srcY,
        int srcWidth,
        int srcHeight,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr) => DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttr, null);

    public void DrawImage(
        Image image,
        Rectangle destRect,
        int srcX,
        int srcY,
        int srcWidth,
        int srcHeight,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttr,
        DrawImageAbort? callback) => DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttr, callback, IntPtr.Zero);

    public void DrawImage(
        Image image,
        Rectangle destRect,
        int srcX,
        int srcY,
        int srcWidth,
        int srcHeight,
        GraphicsUnit srcUnit,
        ImageAttributes? imageAttrs,
        DrawImageAbort? callback,
        IntPtr callbackData) => DrawImage(image, destRect, (float)srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs, callback, callbackData);

    /// <summary>
    ///  Draws a line connecting the two specified points.
    /// </summary>
    public void DrawLine(Pen pen, PointF pt1, PointF pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

    /// <inheritdoc cref="DrawLines(Pen, Point[])"/>
    public void DrawLines(Pen pen, params PointF[] points) => DrawLines(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawLines(Pen, Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawLines(Pen pen, params ReadOnlySpan<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        for (int i = 1; i < points.Length; i++)
        {
            _backend?.StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, pen.Color, pen.Width);
        }

        if (_backend is null)
        {
            throw GdiPlusRemoved(nameof(DrawLines));
        }
    }

    /// <summary>
    ///  Draws a line connecting the two specified points.
    /// </summary>
    public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) =>
        DrawLine(pen, (float)x1, y1, x2, y2);

    /// <summary>
    ///  Draws a line connecting the two specified points.
    /// </summary>
    public void DrawLine(Pen pen, Point pt1, Point pt2) => DrawLine(pen, (float)pt1.X, pt1.Y, pt2.X, pt2.Y);

    /// <summary>
    ///  Draws a series of line segments that connect an array of points.
    /// </summary>
    /// <param name="pen">The <see cref="Pen"/> that determines the color, width, and style of the line segments.</param>
    /// <param name="points">An array of points to connect.</param>
    public void DrawLines(Pen pen, params Point[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);
        DrawLines(pen, points.AsSpan());
    }

    /// <inheritdoc cref="DrawLines(Pen, Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawLines(Pen pen, params ReadOnlySpan<Point> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        for (int i = 1; i < points.Length; i++)
        {
            _backend?.StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, pen.Color, pen.Width);
        }

        if (_backend is null)
        {
            throw GdiPlusRemoved(nameof(DrawLines));
        }
    }

    /// <summary>
    ///  CopyPixels will perform a gdi "bitblt" operation to the source from the destination with the given size.
    /// </summary>
    public void CopyFromScreen(Point upperLeftSource, Point upperLeftDestination, Size blockRegionSize) =>
        CopyFromScreen(upperLeftSource.X, upperLeftSource.Y, upperLeftDestination.X, upperLeftDestination.Y, blockRegionSize);

    /// <summary>
    ///  CopyPixels will perform a gdi "bitblt" operation to the source from the destination with the given size.
    /// </summary>
    public void CopyFromScreen(int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize) =>
        CopyFromScreen(sourceX, sourceY, destinationX, destinationY, blockRegionSize, CopyPixelOperation.SourceCopy);

    /// <summary>
    ///  CopyPixels will perform a gdi "bitblt" operation to the source from the destination with the given size
    ///  and specified raster operation.
    /// </summary>
    public void CopyFromScreen(Point upperLeftSource, Point upperLeftDestination, Size blockRegionSize, CopyPixelOperation copyPixelOperation) =>
        CopyFromScreen(upperLeftSource.X, upperLeftSource.Y, upperLeftDestination.X, upperLeftDestination.Y, blockRegionSize, copyPixelOperation);

    public void EnumerateMetafile(Metafile metafile, PointF destPoint, EnumerateMetafileProc callback) =>
        EnumerateMetafile(metafile, destPoint, callback, IntPtr.Zero);

    public void EnumerateMetafile(Metafile metafile, PointF destPoint, EnumerateMetafileProc callback, IntPtr callbackData) =>
        EnumerateMetafile(metafile, destPoint, callback, callbackData, null);

    public void EnumerateMetafile(Metafile metafile, Point destPoint, EnumerateMetafileProc callback) =>
        EnumerateMetafile(metafile, destPoint, callback, IntPtr.Zero);

    public void EnumerateMetafile(Metafile metafile, Point destPoint, EnumerateMetafileProc callback, IntPtr callbackData) =>
        EnumerateMetafile(metafile, destPoint, callback, callbackData, null);

    public void EnumerateMetafile(Metafile metafile, RectangleF destRect, EnumerateMetafileProc callback) =>
        EnumerateMetafile(metafile, destRect, callback, IntPtr.Zero);

    public void EnumerateMetafile(Metafile metafile, RectangleF destRect, EnumerateMetafileProc callback, IntPtr callbackData) =>
        EnumerateMetafile(metafile, destRect, callback, callbackData, null);

    public void EnumerateMetafile(Metafile metafile, Rectangle destRect, EnumerateMetafileProc callback) =>
        EnumerateMetafile(metafile, destRect, callback, IntPtr.Zero);

    public void EnumerateMetafile(Metafile metafile, Rectangle destRect, EnumerateMetafileProc callback, IntPtr callbackData) =>
        EnumerateMetafile(metafile, destRect, callback, callbackData, null);

    public void EnumerateMetafile(Metafile metafile, PointF[] destPoints, EnumerateMetafileProc callback) =>
        EnumerateMetafile(metafile, destPoints, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF[] destPoints,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destPoints, callback, IntPtr.Zero, null);

    public void EnumerateMetafile(Metafile metafile, Point[] destPoints, EnumerateMetafileProc callback) =>
        EnumerateMetafile(metafile, destPoints, callback, IntPtr.Zero);

    public void EnumerateMetafile(Metafile metafile, Point[] destPoints, EnumerateMetafileProc callback, IntPtr callbackData) =>
        EnumerateMetafile(metafile, destPoints, callback, callbackData, null);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF destPoint,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback) => EnumerateMetafile(metafile, destPoint, srcRect, srcUnit, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF destPoint,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destPoint, srcRect, srcUnit, callback, callbackData, null);

    public void EnumerateMetafile(
        Metafile metafile,
        Point destPoint,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback) => EnumerateMetafile(metafile, destPoint, srcRect, srcUnit, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        Point destPoint,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destPoint, srcRect, srcUnit, callback, callbackData, null);

    public void EnumerateMetafile(
        Metafile metafile,
        RectangleF destRect,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback) => EnumerateMetafile(metafile, destRect, srcRect, srcUnit, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        RectangleF destRect,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destRect, srcRect, srcUnit, callback, callbackData, null);

    public void EnumerateMetafile(
        Metafile metafile,
        Rectangle destRect,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback) => EnumerateMetafile(metafile, destRect, srcRect, srcUnit, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        Rectangle destRect,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destRect, srcRect, srcUnit, callback, callbackData, null);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF[] destPoints,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback) => EnumerateMetafile(metafile, destPoints, srcRect, srcUnit, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF[] destPoints,
        RectangleF srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destPoints, srcRect, srcUnit, callback, callbackData, null);

    public void EnumerateMetafile(
        Metafile metafile,
        Point[] destPoints,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback) => EnumerateMetafile(metafile, destPoints, srcRect, srcUnit, callback, IntPtr.Zero);

    public void EnumerateMetafile(
        Metafile metafile,
        Point[] destPoints,
        Rectangle srcRect,
        GraphicsUnit srcUnit,
        EnumerateMetafileProc callback,
        IntPtr callbackData) => EnumerateMetafile(metafile, destPoints, srcRect, srcUnit, callback, callbackData, null);

    /// <summary>
    ///  Transforms an array of points from one coordinate space to another using the current world and page
    ///  transformations of this <see cref="Graphics"/>.
    /// </summary>
    /// <param name="destSpace">The destination coordinate space.</param>
    /// <param name="srcSpace">The source coordinate space.</param>
    /// <param name="pts">The points to transform.</param>
    public void TransformPoints(Drawing2D.CoordinateSpace destSpace, Drawing2D.CoordinateSpace srcSpace, params PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        TransformPointSpan(destSpace, srcSpace, pts.AsSpan());
    }

    /// <inheritdoc cref="TransformPoints(Drawing2D.CoordinateSpace, Drawing2D.CoordinateSpace, PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void TransformPoints(Drawing2D.CoordinateSpace destSpace, Drawing2D.CoordinateSpace srcSpace, params ReadOnlySpan<PointF> pts)
    {
        if (srcSpace != destSpace && !_transformElements.IsIdentity)
            throw GdiPlusRemoved(nameof(TransformPoints));
    }

    /// <inheritdoc cref="TransformPoints(Drawing2D.CoordinateSpace, Drawing2D.CoordinateSpace, PointF[])"/>
    public void TransformPoints(Drawing2D.CoordinateSpace destSpace, Drawing2D.CoordinateSpace srcSpace, params Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        TransformPointSpan(destSpace, srcSpace, pts.AsSpan());
    }

    /// <inheritdoc cref="TransformPoints(Drawing2D.CoordinateSpace, Drawing2D.CoordinateSpace, PointF[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void TransformPoints(Drawing2D.CoordinateSpace destSpace, Drawing2D.CoordinateSpace srcSpace, params ReadOnlySpan<Point> pts)
    {
        if (srcSpace != destSpace && !_transformElements.IsIdentity)
            throw GdiPlusRemoved(nameof(TransformPoints));
    }

    private void TransformPointSpan(Drawing2D.CoordinateSpace destSpace, Drawing2D.CoordinateSpace srcSpace, Span<PointF> pts)
    {
        if (srcSpace == destSpace || _transformElements.IsIdentity)
        {
            return;
        }

        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 transformed = Vector2.Transform(new Vector2(pts[i].X, pts[i].Y), _transformElements);
            pts[i] = new PointF(transformed.X, transformed.Y);
        }
    }

    private void TransformPointSpan(Drawing2D.CoordinateSpace destSpace, Drawing2D.CoordinateSpace srcSpace, Span<Point> pts)
    {
        if (srcSpace == destSpace || _transformElements.IsIdentity)
        {
            return;
        }

        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 transformed = Vector2.Transform(new Vector2(pts[i].X, pts[i].Y), _transformElements);
            pts[i] = new Point((int)MathF.Round(transformed.X), (int)MathF.Round(transformed.Y));
        }
    }

    /// <summary>
    ///  GDI+ will return a 'generic error' when we attempt to draw an Emf
    ///  image with width/height == 1. Here, we will hack around this by
    ///  resetting the Status. Note that we don't do simple arg checking
    ///  for height || width == 1 here because transforms can be applied to
    ///  the Graphics object making it difficult to identify this scenario.
    /// </summary>
    private static void IgnoreMetafileErrors(Image image, ref Status errorStatus)
    {
        if (errorStatus != Status.Ok && image.RawFormat.Equals(ImageFormat.Emf))
            errorStatus = Status.Ok;

        GC.KeepAlive(image);
    }

    /// <summary>
    ///  Creates a Region class only if the native region is not infinite.
    /// </summary>
    internal Region? GetRegionIfNotInfinite()
    {
        return _clip?.Clone();
    }

    /// <summary>
    ///  Represents an object used in connection with the printing API, it is used to hold a reference to a
    ///  PrintPreviewGraphics (fake graphics) or a printer DeviceContext (and maybe more in the future).
    /// </summary>
    internal object? PrintingHelper
    {
        get => _printingHelper;
        set
        {
            Debug.Assert(_printingHelper is null, "WARNING: Overwriting the printing helper reference!");
            _printingHelper = value;
        }
    }

    /// <summary>
    ///  CopyPixels will perform a gdi "bitblt" operation to the source from the destination with the given size
    ///  and specified raster operation.
    /// </summary>
    public void CopyFromScreen(int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize, CopyPixelOperation copyPixelOperation)
    {
        switch (copyPixelOperation)
        {
            case CopyPixelOperation.Blackness:
            case CopyPixelOperation.NotSourceErase:
            case CopyPixelOperation.NotSourceCopy:
            case CopyPixelOperation.SourceErase:
            case CopyPixelOperation.DestinationInvert:
            case CopyPixelOperation.PatInvert:
            case CopyPixelOperation.SourceInvert:
            case CopyPixelOperation.SourceAnd:
            case CopyPixelOperation.MergePaint:
            case CopyPixelOperation.MergeCopy:
            case CopyPixelOperation.SourceCopy:
            case CopyPixelOperation.SourcePaint:
            case CopyPixelOperation.PatCopy:
            case CopyPixelOperation.PatPaint:
            case CopyPixelOperation.Whiteness:
            case CopyPixelOperation.CaptureBlt:
            case CopyPixelOperation.NoMirrorBitmap:
                break;
            default:
                throw new InvalidEnumArgumentException(nameof(copyPixelOperation), (int)copyPixelOperation, typeof(CopyPixelOperation));
        }

        throw GdiPlusRemoved(nameof(CopyFromScreen));
    }

    public Color GetNearestColor(Color color)
    {
        return color;
    }

    /// <summary>
    ///  Draws a line connecting the two specified points.
    /// </summary>
    public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
    {
        ArgumentNullException.ThrowIfNull(pen);
        
        if (_backend is not null)
        {
            Color color = pen.Color;
            float width = pen.Width > 0 ? pen.Width : 1f;
            _backend.StrokeLine(x1, y1, x2, y2, color, width);
            return;
        }

        throw GdiPlusRemoved(nameof(DrawLine));
    }

    /// <inheritdoc cref="DrawBeziers(Pen, Point[])"/>
    public void DrawBeziers(Pen pen, params PointF[] points) =>
        DrawBeziers(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawBeziers(Pen, Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawBeziers(Pen pen, params ReadOnlySpan<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        DrawLines(pen, points);
    }

    /// <summary>
    ///  Draws a series of cubic Bézier curves from an array of points.
    /// </summary>
    /// <param name="pen">The <paramref name="pen"/> to draw the Bézier with.</param>
    /// <param name="points">
    ///  Points that represent the points that determine the curve. The number of points in the array
    ///  should be a multiple of 3 plus 1, such as 4, 7, or 10.
    /// </param>
    public void DrawBeziers(Pen pen, params Point[] points) => DrawBeziers(pen, points.OrThrowIfNull().AsSpan());

    /// <inheritdoc cref="DrawBeziers(Pen, Point[])"/>
#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void DrawBeziers(Pen pen, params ReadOnlySpan<Point> points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        DrawLines(pen, points);
    }

    /// <summary>
    ///  Fills the interior of a path.
    /// </summary>
    public void FillPath(Brush brush, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(path);

        throw GdiPlusRemoved(nameof(FillPath));
    }

    /// <summary>
    ///  Fills the interior of a <see cref='Region'/>.
    /// </summary>
    public void FillRegion(Brush brush, Region region)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(region);

        throw GdiPlusRemoved(nameof(FillRegion));
    }

    public void DrawIcon(Icon icon, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (_backingImage is not null)
        {
            // We don't call the icon directly because we want to stay in GDI+ all the time
            // to avoid alpha channel interop issues between gdi and gdi+
            // so we do icon.ToBitmap() and then we call DrawImage. This is probably slower.
            DrawImage(icon.ToBitmap(), x, y);
        }
        else
        {
            icon.Draw(this, x, y);
        }
    }

    /// <summary>
    ///  Draws this image to a graphics object. The drawing command originates on the graphics
    ///  object, but a graphics object generally has no idea how to render a given image. So,
    ///  it passes the call to the actual image. This version crops the image to the given
    ///  dimensions and allows the user to specify a rectangle within the image to draw.
    /// </summary>
    public void DrawIcon(Icon icon, Rectangle targetRect)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (_backingImage is not null)
        {
            // We don't call the icon directly because we want to stay in GDI+ all the time
            // to avoid alpha channel interop issues between gdi and gdi+
            // so we do icon.ToBitmap() and then we call DrawImage. This is probably slower.
            DrawImage(icon.ToBitmap(), targetRect);
        }
        else
        {
            icon.Draw(this, targetRect);
        }
    }

    /// <summary>
    ///  Draws this image to a graphics object. The drawing command originates on the graphics
    ///  object, but a graphics object generally has no idea how to render a given image. So,
    ///  it passes the call to the actual image. This version stretches the image to the given
    ///  dimensions and allows the user to specify a rectangle within the image to draw.
    /// </summary>
    public void DrawIconUnstretched(Icon icon, Rectangle targetRect)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (_backingImage is not null)
        {
            DrawImageUnscaled(icon.ToBitmap(), targetRect);
        }
        else
        {
            icon.DrawUnstretched(this, targetRect);
        }
    }

    public void EnumerateMetafile(
        Metafile metafile,
        PointF destPoint,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        Point destPoint,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
        => EnumerateMetafile(metafile, (PointF)destPoint, callback, callbackData, imageAttr);

    public void EnumerateMetafile(
        Metafile metafile,
        RectangleF destRect,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        Rectangle destRect,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr) => EnumerateMetafile(metafile, (RectangleF)destRect, callback, callbackData, imageAttr);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF[] destPoints,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(destPoints);

        if (destPoints.Length != 3)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidParallelogram);

        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        Point[] destPoints,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(destPoints);

        if (destPoints.Length != 3)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidParallelogram);

        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        PointF destPoint,
        RectangleF srcRect,
        GraphicsUnit unit,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        Point destPoint,
        Rectangle srcRect,
        GraphicsUnit unit,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr) => EnumerateMetafile(
            metafile,
            (PointF)destPoint,
            (RectangleF)srcRect,
            unit,
            callback,
            callbackData,
            imageAttr);

    public void EnumerateMetafile(
        Metafile metafile,
        RectangleF destRect,
        RectangleF srcRect,
        GraphicsUnit unit,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        Rectangle destRect,
        Rectangle srcRect,
        GraphicsUnit unit,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr) => EnumerateMetafile(metafile, (RectangleF)destRect, srcRect, unit, callback, callbackData, imageAttr);

    public void EnumerateMetafile(
        Metafile metafile,
        PointF[] destPoints,
        RectangleF srcRect,
        GraphicsUnit unit,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(destPoints);

        if (destPoints.Length != 3)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidParallelogram);

        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    public void EnumerateMetafile(
        Metafile metafile,
        Point[] destPoints,
        Rectangle srcRect,
        GraphicsUnit unit,
        EnumerateMetafileProc callback,
        IntPtr callbackData,
        ImageAttributes? imageAttr)
    {
        ArgumentNullException.ThrowIfNull(destPoints);

        if (destPoints.Length != 3)
            throw new ArgumentException(SR.GdiplusDestPointsInvalidParallelogram);

        ArgumentNullException.ThrowIfNull(metafile);
        ArgumentNullException.ThrowIfNull(callback);
        throw GdiPlusRemoved(nameof(EnumerateMetafile));
    }

    /// <summary>
    ///  Combines current Graphics context with all previous contexts.
    ///  When BeginContainer() is called, a copy of the current context is pushed into the GDI+ context stack, it keeps track of the
    ///  absolute clipping and transform but reset the public properties so it looks like a brand new context.
    ///  When Save() is called, a copy of the current context is also pushed in the GDI+ stack but the public clipping and transform
    ///  properties are not reset (cumulative). Consecutive Save context are ignored with the exception of the top one which contains
    ///  all previous information.
    ///  The return value is an object array where the first element contains the cumulative clip region and the second the cumulative
    ///  translate transform matrix.
    ///  WARNING: This method is for internal FX support only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use the Graphics.GetContextInfo overloads that accept arguments for better performance and fewer allocations.", DiagnosticId = "SYSLIB0016", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    [SupportedOSPlatform("windows")]
    public object GetContextInfo()
    {
        GetContextInfo(out Matrix3x2 cumulativeTransform, calculateClip: true, out Region? cumulativeClip);
        return new object[] { cumulativeClip ?? new Region(), new Matrix(cumulativeTransform) };
    }

    private void GetContextInfo(out Matrix3x2 cumulativeTransform, bool calculateClip, out Region? cumulativeClip)
    {
        cumulativeClip = calculateClip ? GetRegionIfNotInfinite() : null;   // Current context clip.
        cumulativeTransform = TransformElements;                            // Current context transform.
        Vector2 currentOffset = default;                                    // Offset of current context.
        Vector2 totalOffset = default;                                      // Absolute coordinate offset of top context.

        GraphicsContext? context = _previousContext;

        if (!cumulativeTransform.IsIdentity)
        {
            currentOffset = cumulativeTransform.Translation;
        }

        while (context is not null)
        {
            if (!context.TransformOffset.IsEmpty())
            {
                cumulativeTransform.Translate(context.TransformOffset);
            }

            if (!currentOffset.IsEmpty())
            {
                // The location of the GDI+ clip region is relative to the coordinate origin after any translate transform
                // has been applied. We need to intersect regions using the same coordinate origin relative to the previous
                // context.

                // If we don't have a cumulative clip, we're infinite, and translation on infinite regions is a no-op.
                cumulativeClip?.Translate(currentOffset.X, currentOffset.Y);
                totalOffset.X += currentOffset.X;
                totalOffset.Y += currentOffset.Y;
            }

            // Context only stores clips if they are not infinite. Intersecting a clip with an infinite clip is a no-op.
            if (calculateClip && context.Clip is not null)
            {
                // Intersecting an infinite clip with another is just a copy of the second clip.
                if (cumulativeClip is null)
                {
                    cumulativeClip = context.Clip;
                }
                else
                {
                    cumulativeClip.Intersect(context.Clip);
                }
            }

            currentOffset = context.TransformOffset;

            // Ignore subsequent cumulative contexts.
            do
            {
                context = context.Previous;

                if (context is null || !context.Next!.IsCumulative)
                {
                    break;
                }
            }
            while (context.IsCumulative);
        }

        if (!totalOffset.IsEmpty())
        {
            // We need now to reset the total transform in the region so when calling Region.GetHRgn(Graphics)
            // the HRegion is properly offset by GDI+ based on the total offset of the graphics object.

            // If we don't have a cumulative clip, we're infinite, and translation on infinite regions is a no-op.
            cumulativeClip?.Translate(-totalOffset.X, -totalOffset.Y);
        }
    }

    (HDC hdc, int saveState) IGraphicsContextInfo.GetHdc(ApplyGraphicsProperties apply, bool alwaysSaveState)
    {
        return ((HDC)GetHdc(), 0);
    }

    /// <summary>
    ///  Gets the cumulative offset.
    /// </summary>
    /// <param name="offset">The cumulative offset.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SupportedOSPlatform("windows")]
    public void GetContextInfo(out PointF offset)
    {
        GetContextInfo(out Matrix3x2 cumulativeTransform, calculateClip: false, out _);
        Vector2 translation = cumulativeTransform.Translation;
        offset = new PointF(translation.X, translation.Y);
    }

    /// <summary>
    ///  Gets the cumulative offset and clip region.
    /// </summary>
    /// <param name="offset">The cumulative offset.</param>
    /// <param name="clip">The cumulative clip region or null if the clip region is infinite.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [SupportedOSPlatform("windows")]
    public void GetContextInfo(out PointF offset, out Region? clip)
    {
        GetContextInfo(out Matrix3x2 cumulativeTransform, calculateClip: true, out clip);
        Vector2 translation = cumulativeTransform.Translation;
        offset = new PointF(translation.X, translation.Y);
    }

    public RectangleF VisibleClipBounds
    {
        get
        {
            if (PrintingHelper is PrintPreviewGraphics ppGraphics)
                return ppGraphics.VisibleClipBounds;

            return _clipBounds;
        }
    }

    /// <summary>
    ///  Saves the current context into the context stack.
    /// </summary>
    private void PushContext(GraphicsContext context)
    {
        Debug.Assert(context is not null && context.State != 0, "GraphicsContext object is null or not valid.");

        if (_previousContext is not null)
        {
            // Push context.
            context.Previous = _previousContext;
            _previousContext.Next = context;
        }

        _previousContext = context;
    }

    /// <summary>
    ///  Pops all contexts from the specified one included. The specified context is becoming the current context.
    /// </summary>
    private void PopContext(int currentContextState)
    {
        Debug.Assert(_previousContext is not null, "Trying to restore a context when the stack is empty");
        GraphicsContext? context = _previousContext;

        // Pop all contexts up the stack.
        while (context is not null)
        {
            if (context.State == currentContextState)
            {
                _previousContext = context.Previous;

                // This will dispose all context object up the stack.
                context.Dispose();
                return;
            }

            context = context.Previous;
        }

        Debug.Fail("Warning: context state not found!");
    }

    public GraphicsState Save()
    {
        _backend?.Save();
        GraphicsContext context = new(this);
        int state = _nextContextState++;
        context.State = state;
        context.IsCumulative = true;
        PushContext(context);
        return new GraphicsState(state);
    }

    public void Restore(GraphicsState gstate)
    {
        _backend?.Restore(); // WinForms usually restores in order, so Restore() is typically enough. If it's a deep restore, this might need RestoreToCount, but Impeller backend does not have a 1:1 mapping of state IDs.
        PopContext(gstate._nativeState);
    }

    public GraphicsContainer BeginContainer(RectangleF dstrect, RectangleF srcrect, GraphicsUnit unit)
    {
        GraphicsContext context = new(this);
        int state = _nextContextState++;
        context.State = state;
        PushContext(context);
        return new GraphicsContainer(state);
    }

    public GraphicsContainer BeginContainer()
    {
        GraphicsContext context = new(this);
        int state = _nextContextState++;
        context.State = state;
        PushContext(context);
        return new GraphicsContainer(state);
    }

    public void EndContainer(GraphicsContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        PopContext(container._nativeGraphicsContainer);
    }

    public GraphicsContainer BeginContainer(Rectangle dstrect, Rectangle srcrect, GraphicsUnit unit)
        => BeginContainer((RectangleF)dstrect, (RectangleF)srcrect, unit);

    public void AddMetafileComment(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        throw GdiPlusRemoved(nameof(AddMetafileComment));
    }

    public static IntPtr GetHalftonePalette()
    {
        if (s_halftonePalette == IntPtr.Zero)
        {
            lock (s_syncObject)
            {
                if (s_halftonePalette == IntPtr.Zero)
                {
                    s_halftonePalette = HPALETTE.Null;
                }
            }
        }

        return s_halftonePalette;
    }

    // This is called from AppDomain.ProcessExit and AppDomain.DomainUnload.
    private static void OnDomainUnload(object? sender, EventArgs e)
    {
        s_halftonePalette = HPALETTE.Null;
    }

    /// <summary>
    ///  GDI+ will return a 'generic error' with specific win32 last error codes when
    ///  a terminal server session has been closed, minimized, etc... We don't want
    ///  to throw when this happens, so we'll guard against this by looking at the
    ///  'last win32 error code' and checking to see if it is either 1) access denied
    ///  or 2) proc not found and then ignore it.
    ///
    ///  The problem is that when you lock the machine, the secure desktop is enabled and
    ///  rendering fails which is expected (since the app doesn't have permission to draw
    ///  on the secure desktop). Not sure if there's anything you can do, short of catching
    ///  the desktop switch message and absorbing all the exceptions that get thrown while
    ///  it's the secure desktop.
    /// </summary>
    private void CheckErrorStatus(Status status)
    {
        if (status == Status.Ok)
        {
            return;
        }

        throw new InvalidOperationException($"Drawing PAL operation failed with status {status}.");
    }

#if NET8_0_OR_GREATER

    /// <summary>
    ///  Draws the given <paramref name="cachedBitmap"/>.
    /// </summary>
    /// <param name="cachedBitmap">The <see cref="CachedBitmap"/> that contains the image to be drawn.</param>
    /// <param name="x">The x-coordinate of the upper-left corner of the drawn image.</param>
    /// <param name="y">The y-coordinate of the upper-left corner of the drawn image.</param>
    /// <exception cref="ArgumentNullException">
    ///  <para><paramref name="cachedBitmap"/> is <see langword="null"/>.</para>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///  <para>
    ///   The <paramref name="cachedBitmap"/> is not compatible with the <see cref="Graphics"/> device state.
    ///  </para>
    ///  <para>
    ///  - or -
    ///  </para>
    ///  <para>
    ///   The <see cref="Graphics"/> object has a transform applied other than a translation.
    ///  </para>
    /// </exception>
    public void DrawCachedBitmap(CachedBitmap cachedBitmap, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(cachedBitmap);
        throw GdiPlusRemoved(nameof(DrawCachedBitmap));
    }
#endif

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="DrawImage(Image, Effect, RectangleF, Matrix?, GraphicsUnit, ImageAttributes?)"/>
    public void DrawImage(
        Image image,
        Effect effect) => DrawImage(image, effect, srcRect: default, transform: default, GraphicsUnit.Pixel, imageAttr: null);

    /// <summary>
    ///  Draws a portion of an image after applying a specified effect.
    /// </summary>
    /// <param name="image"><see cref="Image"/> to be drawn.</param>
    /// <param name="effect">The effect to be applied when drawing.</param>
    /// <param name="srcRect">The portion of the image to be drawn. <see cref="RectangleF.Empty"/> draws the full image.</param>
    /// <param name="transform">The transform to apply to the <paramref name="srcRect"/> to determine the destination.</param>
    /// <param name="srcUnit">Unit of measure of the <paramref name="srcRect"/>.</param>
    /// <param name="imageAttr">Additional adjustments to be applied, if any.</param>
    public void DrawImage(
        Image image,
        Effect effect,
        RectangleF srcRect = default,
        Matrix? transform = default,
        GraphicsUnit srcUnit = GraphicsUnit.Pixel,
        ImageAttributes? imageAttr = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(effect);
        throw GdiPlusRemoved(nameof(DrawImage));
    }
#endif

    private void CheckStatus(Status status)
    {
        status.ThrowIfFailed();
        GC.KeepAlive(this);
    }
}

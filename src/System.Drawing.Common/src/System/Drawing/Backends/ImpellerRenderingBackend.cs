// Impeller rendering backend — records drawing commands via DisplayListBuilder
// and renders them to the GPU via Impeller's Vulkan/Metal surface.

#if !BROWSER

using System.Drawing.Impeller;

namespace System.Drawing;

/// <summary>
/// Desktop rendering backend. Uses Impeller's DisplayListBuilder for GPU-accelerated
/// rendering via Vulkan or Metal.
/// </summary>
internal sealed class ImpellerRenderingBackend : IRenderingBackend
    {
        private readonly IPlatformBackend _platformBackend;
        private readonly nint _impellerContext;
        private static readonly HarfBuzzTextEngine s_textEngine = new();
        private DisplayListBuilder? _builder;
        private nint _frameSurface;

    public ImpellerRenderingBackend(IPlatformBackend platformBackend, nint impellerContext)
    {
        _platformBackend = platformBackend;
        _impellerContext = impellerContext;
    }

    /// <summary>The Impeller context, needed for texture creation in Image.</summary>
    internal nint ImpellerContext => _impellerContext;

    // ─── Frame Lifecycle ────────────────────────────────────────────────

    public bool BeginFrame(int width, int height)
    {
        _frameSurface = _platformBackend.AcquireNextSurface();
        if (_frameSurface == nint.Zero)
            return false;
        _builder = new DisplayListBuilder();
        return true;
    }

    public void EndFrame(int width, int height)
    {
        if (_builder is null)
            return;

        // Guard: surface may have been invalidated by a resize event
        if (_frameSurface == nint.Zero)
        {
            _builder.Dispose();
            _builder = null;
            return;
        }

        var displayList = _builder.Build();
        try
        {
            NativeMethods.ImpellerSurfaceDrawDisplayList(_frameSurface, displayList);
            _platformBackend.PresentSurface(_frameSurface);
        }
        finally
        {
            NativeMethods.ImpellerDisplayListRelease(displayList);
            _frameSurface = nint.Zero;
            _builder.Dispose();
            _builder = null;
        }
    }

    // ─── State Stack ────────────────────────────────────────────────────

    public void Save() => _builder?.Save();
    public void Restore() => _builder?.Restore();
    public uint SaveCount => _builder?.SaveCount ?? 0;
    public void RestoreToCount(uint count) => _builder?.RestoreToCount(count);

    // ─── Transforms ─────────────────────────────────────────────────────

    public void Translate(float dx, float dy) => _builder?.Translate(dx, dy);
    public void Scale(float sx, float sy) => _builder?.Scale(sx, sy);
    public void Rotate(float radians) => _builder?.Rotate(radians * 180f / MathF.PI); // Impeller uses degrees
    public void ResetTransform() => _builder?.ResetTransform();

    // ─── Clipping ───────────────────────────────────────────────────────

    public void ClipRect(float x, float y, float w, float h)
    {
        if (_builder is null)
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _builder.ClipRect(ref rect);
    }

    // ─── Fill Operations ────────────────────────────────────────────────

    public void Clear(Color color)
    {
        if (_builder is null)
            return;
        var paintHandle = NativeMethods.ImpellerPaintNew();
        try
        {
            var ic = color.ToImpellerColor();
            NativeMethods.ImpellerPaintSetColor(paintHandle, ref ic);
            NativeMethods.ImpellerPaintSetDrawStyle(paintHandle, ImpellerDrawStyle.Fill);
            _builder.DrawPaint(paintHandle);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void FillRect(float x, float y, float w, float h, Color color)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateFillPaint(color);
        try
        {
            var rect = new ImpellerRect(x, y, w, h);
            _builder.DrawRect(ref rect, paintHandle);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void FillEllipse(float x, float y, float w, float h, Color color)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateFillPaint(color);
        try
        {
            var rect = new ImpellerRect(x, y, w, h);
            _builder.DrawOval(ref rect, paintHandle);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void FillPolygon(Point[] points, Color color)
    {
        if (_builder is null || points.Length < 3)
            return;
        var paintHandle = CreateFillPaint(color);
        try
        {
            var pathBuilder = NativeMethods.ImpellerPathBuilderNew();
            var p0 = new ImpellerPoint(points[0].X, points[0].Y);
            NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
            for (int i = 1; i < points.Length; i++)
            {
                var p = new ImpellerPoint(points[i].X, points[i].Y);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p);
            }

            NativeMethods.ImpellerPathBuilderClose(pathBuilder);
            var path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
            NativeMethods.ImpellerPathRelease(path);
            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    // ─── Stroke Operations ──────────────────────────────────────────────

    public void StrokeRect(float x, float y, float w, float h, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateStrokePaint(color, lineWidth);
        try
        {
            var rect = new ImpellerRect(x, y, w, h);
            _builder.DrawRect(ref rect, paintHandle);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void StrokeEllipse(float x, float y, float w, float h, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateStrokePaint(color, lineWidth);
        try
        {
            var rect = new ImpellerRect(x, y, w, h);
            _builder.DrawOval(ref rect, paintHandle);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void StrokeLine(float x1, float y1, float x2, float y2, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateStrokePaint(color, lineWidth);
        try
        {
            var from = new ImpellerPoint(x1, y1);
            var to = new ImpellerPoint(x2, y2);
            _builder.DrawLine(ref from, ref to, paintHandle);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void StrokePolygon(Point[] points, Color color, float lineWidth)
    {
        if (_builder is null || points.Length < 3)
            return;
        var paintHandle = CreateStrokePaint(color, lineWidth);
        try
        {
            var pathBuilder = NativeMethods.ImpellerPathBuilderNew();
            var p0 = new ImpellerPoint(points[0].X, points[0].Y);
            NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
            for (int i = 1; i < points.Length; i++)
            {
                var p = new ImpellerPoint(points[i].X, points[i].Y);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p);
            }

            NativeMethods.ImpellerPathBuilderClose(pathBuilder);
            var path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
            NativeMethods.ImpellerPathRelease(path);
            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    // ─── Text ───────────────────────────────────────────────────────────

        public void DrawString(string text, float x, float y, Color color,
                               string fontFamily, float fontSize, bool bold, bool italic)
        {
            s_textEngine.DrawString(this, text, x, y, color, fontFamily, fontSize, bold, italic);
        }

        public void DrawStringAligned(string text, RectangleF bounds, ContentAlignment alignment,
                                      Color color, string fontFamily, float fontSize, bool bold, bool italic)
        {
            s_textEngine.DrawStringAligned(this, text, bounds, alignment, color, fontFamily, fontSize, bold, italic);
        }

        public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic)
        {
            return s_textEngine.MeasureString(text, fontFamily, fontSize, bold, italic);
        }

    // ─── Paths ──────────────────────────────────────────────────────────

    public void DrawBezier(float x1, float y1, float cx1, float cy1,
                           float cx2, float cy2, float x2, float y2, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateStrokePaint(color, lineWidth);
        try
        {
            var pathBuilder = NativeMethods.ImpellerPathBuilderNew();
            var p0 = new ImpellerPoint(x1, y1);
            NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
            var cp1 = new ImpellerPoint(cx1, cy1);
            var cp2 = new ImpellerPoint(cx2, cy2);
            var end = new ImpellerPoint(x2, y2);
            NativeMethods.ImpellerPathBuilderCubicCurveTo(pathBuilder, ref cp1, ref cp2, ref end);
            var path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
            NativeMethods.ImpellerPathRelease(path);
            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void DrawPath(nint pathHandle, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateStrokePaint(color, lineWidth);
        try
        { _builder.DrawPath(pathHandle, paintHandle); }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    public void FillPath(nint pathHandle, Color color)
    {
        if (_builder is null)
            return;
        var paintHandle = CreateFillPaint(color);
        try
        { _builder.DrawPath(pathHandle, paintHandle); }
        finally { NativeMethods.ImpellerPaintRelease(paintHandle); }
    }

    // ─── Images ─────────────────────────────────────────────────────────

    public void DrawImage(Image image, float x, float y)
    {
        // if (_builder is null) return;
        // var texture = image.GetTextureHandle(_impellerContext);
        // if (texture == nint.Zero) return;
        // var point = new ImpellerPoint(x, y);
        // _builder.DrawTexture(texture, ref point, nint.Zero);
    }

    public void DrawImageRect(Image image, float sx, float sy, float sw, float sh,
                              float dx, float dy, float dw, float dh)
    {
        // if (_builder is null) return;
        // var texture = image.GetTextureHandle(_impellerContext);
        // if (texture == nint.Zero) return;
        // var src = new ImpellerRect(sx, sy, sw, sh);
        // var dst = new ImpellerRect(dx, dy, dw, dh);
        // _builder.DrawTextureRect(texture, ref src, ref dst, nint.Zero);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private static nint CreateFillPaint(Color color)
    {
        var handle = NativeMethods.ImpellerPaintNew();
        var ic = color.ToImpellerColor();
        NativeMethods.ImpellerPaintSetColor(handle, ref ic);
        NativeMethods.ImpellerPaintSetDrawStyle(handle, ImpellerDrawStyle.Fill);
        return handle;
    }

    private static nint CreateStrokePaint(Color color, float lineWidth)
    {
        var handle = NativeMethods.ImpellerPaintNew();
        var ic = color.ToImpellerColor();
        NativeMethods.ImpellerPaintSetColor(handle, ref ic);
        NativeMethods.ImpellerPaintSetDrawStyle(handle, ImpellerDrawStyle.Stroke);
        NativeMethods.ImpellerPaintSetStrokeWidth(handle, lineWidth);
        return handle;
    }
}
#endif

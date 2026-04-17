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
        var displayList = _builder.Build();
        try
        {
            NativeMethods.ImpellerSurfaceDrawDisplayList(_frameSurface, displayList);
            _platformBackend.PresentSurface(_frameSurface);
        }
        finally
        {
            NativeMethods.ImpellerDisplayListRelease(displayList);
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
        if (_builder is null || string.IsNullOrEmpty(text))
            return;
        text = text.Replace("\r\n", "\n");
        var typoCtx = TypographyProvider.Context;
        if (typoCtx == nint.Zero)
            return;

        var paintHandle = CreateFillPaint(color);
        var style = NativeMethods.ImpellerParagraphStyleNew();
        if (style == nint.Zero)
        { NativeMethods.ImpellerPaintRelease(paintHandle); return; }
        try
        {
            NativeMethods.ImpellerParagraphStyleSetForeground(style, paintHandle);
            NativeMethods.ImpellerParagraphStyleSetFontSize(style, fontSize);
            if (!string.IsNullOrEmpty(fontFamily))
                NativeMethods.ImpellerParagraphStyleSetFontFamily(style, fontFamily);
            if (bold)
                NativeMethods.ImpellerParagraphStyleSetFontWeight(style, ImpellerFontWeight.W700);
            if (italic)
                NativeMethods.ImpellerParagraphStyleSetFontStyle(style, ImpellerFontStyle.Italic);
            NativeMethods.ImpellerParagraphStyleSetTextAlignment(style, ImpellerTextAlignment.Left);

            var builder = NativeMethods.ImpellerParagraphBuilderNew(typoCtx);
            if (builder == nint.Zero)
                return;
            try
            {
                NativeMethods.ImpellerParagraphBuilderPushStyle(builder, style);
                var utf8 = System.Text.Encoding.UTF8.GetBytes(text);
                unsafe
                { fixed (byte* p = utf8) { NativeMethods.ImpellerParagraphBuilderAddText(builder, (nint)p, (uint)utf8.Length); } }
                NativeMethods.ImpellerParagraphBuilderPopStyle(builder);
                var paragraph = NativeMethods.ImpellerParagraphBuilderBuildParagraphNew(builder, 10000f);
                if (paragraph == nint.Zero)
                    return;
                try
                {
                    var point = new ImpellerPoint(x, y);
                    _builder.DrawParagraph(paragraph, ref point);
                }
                finally { NativeMethods.ImpellerParagraphRelease(paragraph); }
            }
            finally { NativeMethods.ImpellerParagraphBuilderRelease(builder); }
        }
        finally
        {
            NativeMethods.ImpellerParagraphStyleRelease(style);
            NativeMethods.ImpellerPaintRelease(paintHandle);
        }
    }

    public void DrawStringAligned(string text, RectangleF bounds, ContentAlignment alignment,
                                  Color color, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (_builder is null || string.IsNullOrEmpty(text))
            return;
        text = text.Replace("\r\n", "\n");
        var typoCtx = TypographyProvider.Context;
        if (typoCtx == nint.Zero)
            return;

        var paintHandle = CreateFillPaint(color);
        var style = NativeMethods.ImpellerParagraphStyleNew();
        if (style == nint.Zero)
        { NativeMethods.ImpellerPaintRelease(paintHandle); return; }
        try
        {
            NativeMethods.ImpellerParagraphStyleSetForeground(style, paintHandle);
            NativeMethods.ImpellerParagraphStyleSetFontSize(style, fontSize);
            if (!string.IsNullOrEmpty(fontFamily))
                NativeMethods.ImpellerParagraphStyleSetFontFamily(style, fontFamily);
            if (bold)
                NativeMethods.ImpellerParagraphStyleSetFontWeight(style, ImpellerFontWeight.W700);
            if (italic)
                NativeMethods.ImpellerParagraphStyleSetFontStyle(style, ImpellerFontStyle.Italic);

            var hAlign = alignment switch
            {
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => ImpellerTextAlignment.Center,
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => ImpellerTextAlignment.Right,
                _ => ImpellerTextAlignment.Left
            };
            NativeMethods.ImpellerParagraphStyleSetTextAlignment(style, hAlign);

            var builder = NativeMethods.ImpellerParagraphBuilderNew(typoCtx);
            if (builder == nint.Zero)
                return;
            try
            {
                NativeMethods.ImpellerParagraphBuilderPushStyle(builder, style);
                var utf8 = System.Text.Encoding.UTF8.GetBytes(text);
                unsafe
                { fixed (byte* p = utf8) { NativeMethods.ImpellerParagraphBuilderAddText(builder, (nint)p, (uint)utf8.Length); } }
                NativeMethods.ImpellerParagraphBuilderPopStyle(builder);
                var paragraph = NativeMethods.ImpellerParagraphBuilderBuildParagraphNew(builder, bounds.Width);
                if (paragraph == nint.Zero)
                    return;
                try
                {
                    float pHeight = NativeMethods.ImpellerParagraphGetHeight(paragraph);
                    float yOffset = alignment switch
                    {
                        ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => (bounds.Height - pHeight) / 2f,
                        ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => bounds.Height - pHeight,
                        _ => 0f
                    };
                    var point = new ImpellerPoint(bounds.X, bounds.Y + yOffset);
                    _builder.DrawParagraph(paragraph, ref point);
                }
                finally { NativeMethods.ImpellerParagraphRelease(paragraph); }
            }
            finally { NativeMethods.ImpellerParagraphBuilderRelease(builder); }
        }
        finally
        {
            NativeMethods.ImpellerParagraphStyleRelease(style);
            NativeMethods.ImpellerPaintRelease(paintHandle);
        }
    }

    public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text))
            return SizeF.Empty;
        text = text.Replace("\r\n", "\n");
        var typoCtx = TypographyProvider.Context;
        if (typoCtx == nint.Zero)
            return SizeF.Empty;

        var style = NativeMethods.ImpellerParagraphStyleNew();
        if (style == nint.Zero)
            return SizeF.Empty;
        try
        {
            NativeMethods.ImpellerParagraphStyleSetFontSize(style, fontSize);
            if (!string.IsNullOrEmpty(fontFamily))
                NativeMethods.ImpellerParagraphStyleSetFontFamily(style, fontFamily);
            if (bold)
                NativeMethods.ImpellerParagraphStyleSetFontWeight(style, ImpellerFontWeight.W700);
            if (italic)
                NativeMethods.ImpellerParagraphStyleSetFontStyle(style, ImpellerFontStyle.Italic);

            var builder = NativeMethods.ImpellerParagraphBuilderNew(typoCtx);
            if (builder == nint.Zero)
                return SizeF.Empty;
            try
            {
                NativeMethods.ImpellerParagraphBuilderPushStyle(builder, style);
                var utf8 = System.Text.Encoding.UTF8.GetBytes(text);
                unsafe
                { fixed (byte* p = utf8) { NativeMethods.ImpellerParagraphBuilderAddText(builder, (nint)p, (uint)utf8.Length); } }
                NativeMethods.ImpellerParagraphBuilderPopStyle(builder);
                var paragraph = NativeMethods.ImpellerParagraphBuilderBuildParagraphNew(builder, 10000f);
                if (paragraph == nint.Zero)
                    return SizeF.Empty;
                try
                {
                    float width = NativeMethods.ImpellerParagraphGetLongestLineWidth(paragraph);
                    float height = NativeMethods.ImpellerParagraphGetHeight(paragraph);
                    return new SizeF(width, height);
                }
                finally { NativeMethods.ImpellerParagraphRelease(paragraph); }
            }
            finally { NativeMethods.ImpellerParagraphBuilderRelease(builder); }
        }
        finally { NativeMethods.ImpellerParagraphStyleRelease(style); }
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

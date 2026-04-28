// Impeller rendering backend — records drawing commands via DisplayListBuilder
// and renders them to the GPU via Impeller's Vulkan/Metal surface.

#if !BROWSER

using System.Drawing.Impeller;
using System.Text;

namespace System.Drawing;

/// <summary>
/// Desktop rendering backend. Uses Impeller's DisplayListBuilder for GPU-accelerated
/// rendering via Vulkan or Metal.
/// </summary>
internal sealed class ImpellerRenderingBackend : IRenderingBackend
    {
        private enum PaintKind
        {
            Fill,
            Stroke,
        }

        private readonly record struct PaintKey(int Argb, PaintKind Kind, int StrokeWidthBits);

        private readonly IPlatformBackend _platformBackend;
        private readonly nint _impellerContext;
        private static readonly HarfBuzzTextEngine s_textEngine = new();
        private readonly Dictionary<PaintKey, nint> _framePaintCache = [];
        private readonly List<nint> _frameParagraphs = [];
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
        _framePaintCache.Clear();
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
            ReleaseFrameParagraphs();
            ReleaseFramePaints();
            _builder.Dispose();
            _builder = null;
            return;
        }

        nint displayList = nint.Zero;
        try
        {
            displayList = _builder.Build();
            if (!NativeMethods.ImpellerSurfaceDrawDisplayList(_frameSurface, displayList))
            {
                throw new InvalidOperationException("Impeller failed to draw the display list.");
            }

            if (!_platformBackend.PresentSurface(_frameSurface))
            {
                throw new InvalidOperationException("Impeller failed to present the frame surface.");
            }
        }
        finally
        {
            if (displayList != nint.Zero)
            {
                NativeMethods.ImpellerDisplayListRelease(displayList);
            }

            NativeMethods.ImpellerSurfaceRelease(_frameSurface);
            _frameSurface = nint.Zero;
            ReleaseFrameParagraphs();
            ReleaseFramePaints();
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
        _builder.DrawPaint(GetFillPaint(color));
    }

    public void FillRect(float x, float y, float w, float h, Color color)
    {
        if (_builder is null)
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _builder.DrawRect(ref rect, GetFillPaint(color));
    }

    public void FillEllipse(float x, float y, float w, float h, Color color)
    {
        if (_builder is null)
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _builder.DrawOval(ref rect, GetFillPaint(color));
    }

    public void FillPolygon(Point[] points, Color color)
    {
        if (_builder is null || points.Length < 3)
            return;
        var paintHandle = GetFillPaint(color);
        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        nint path = nint.Zero;
        try
        {
            var p0 = new ImpellerPoint(points[0].X, points[0].Y);
            NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
            for (int i = 1; i < points.Length; i++)
            {
                var p = new ImpellerPoint(points[i].X, points[i].Y);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p);
            }

            NativeMethods.ImpellerPathBuilderClose(pathBuilder);
            path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
        }
        finally
        {
            if (path != nint.Zero)
            {
                NativeMethods.ImpellerPathRelease(path);
            }

            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
    }

    // ─── Stroke Operations ──────────────────────────────────────────────

    public void StrokeRect(float x, float y, float w, float h, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _builder.DrawRect(ref rect, GetStrokePaint(color, lineWidth));
    }

    public void StrokeEllipse(float x, float y, float w, float h, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _builder.DrawOval(ref rect, GetStrokePaint(color, lineWidth));
    }

    public void StrokeLine(float x1, float y1, float x2, float y2, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var from = new ImpellerPoint(x1, y1);
        var to = new ImpellerPoint(x2, y2);
        _builder.DrawLine(ref from, ref to, GetStrokePaint(color, lineWidth));
    }

    public void StrokePolygon(Point[] points, Color color, float lineWidth)
    {
        if (_builder is null || points.Length < 3)
            return;
        var paintHandle = GetStrokePaint(color, lineWidth);
        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        nint path = nint.Zero;
        try
        {
            var p0 = new ImpellerPoint(points[0].X, points[0].Y);
            NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
            for (int i = 1; i < points.Length; i++)
            {
                var p = new ImpellerPoint(points[i].X, points[i].Y);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p);
            }

            NativeMethods.ImpellerPathBuilderClose(pathBuilder);
            path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
        }
        finally
        {
            if (path != nint.Zero)
            {
                NativeMethods.ImpellerPathRelease(path);
            }

            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
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

        internal unsafe bool DrawNativeText(
            string text,
            float x,
            float y,
            float width,
            Color color,
            string fontFamily,
            float fontSize,
            bool bold,
            bool italic,
            float lineHeight)
        {
            if (_builder is null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            string? familyAlias = TypographyProvider.ResolveFontFamily(fontFamily, bold, italic);
            if (familyAlias is null)
            {
                return false;
            }

            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            nint paragraphStyle = nint.Zero;
            nint paragraphBuilder = nint.Zero;
            nint paragraph = nint.Zero;

            try
            {
                paragraphStyle = NativeMethods.ImpellerParagraphStyleNew();
                if (paragraphStyle == nint.Zero)
                {
                    return false;
                }

                NativeMethods.ImpellerParagraphStyleSetForeground(paragraphStyle, GetFillPaint(color));
                NativeMethods.ImpellerParagraphStyleSetFontFamily(paragraphStyle, familyAlias);
                NativeMethods.ImpellerParagraphStyleSetFontSize(paragraphStyle, MathF.Max(1f, fontSize));
                NativeMethods.ImpellerParagraphStyleSetHeight(paragraphStyle, MathF.Max(0.1f, lineHeight / MathF.Max(1f, fontSize)));
                NativeMethods.ImpellerParagraphStyleSetFontWeight(paragraphStyle, bold ? ImpellerFontWeight.Bold : ImpellerFontWeight.Regular);
                NativeMethods.ImpellerParagraphStyleSetFontStyle(paragraphStyle, italic ? ImpellerFontStyle.Italic : ImpellerFontStyle.Normal);
                NativeMethods.ImpellerParagraphStyleSetTextAlignment(paragraphStyle, ImpellerTextAlignment.Left);
                NativeMethods.ImpellerParagraphStyleSetTextDirection(paragraphStyle, ImpellerTextDirection.LeftToRight);

                paragraphBuilder = NativeMethods.ImpellerParagraphBuilderNew(TypographyProvider.Context);
                if (paragraphBuilder == nint.Zero)
                {
                    return false;
                }

                NativeMethods.ImpellerParagraphBuilderPushStyle(paragraphBuilder, paragraphStyle);
                fixed (byte* textBytes = utf8)
                {
                    NativeMethods.ImpellerParagraphBuilderAddText(paragraphBuilder, textBytes, (uint)utf8.Length);

                    NativeMethods.ImpellerParagraphBuilderPopStyle(paragraphBuilder);

                    paragraph = NativeMethods.ImpellerParagraphBuilderBuildParagraphNew(paragraphBuilder, MathF.Max(1f, width));
                }

                if (paragraph == nint.Zero)
                {
                    return false;
                }

                ImpellerPoint point = new(x, y);
                NativeMethods.ImpellerDisplayListBuilderDrawParagraph(_builder.Handle, paragraph, ref point);
                _frameParagraphs.Add(paragraph);
                paragraph = nint.Zero;
                return true;
            }
            finally
            {
                if (paragraph != nint.Zero)
                {
                    NativeMethods.ImpellerParagraphRelease(paragraph);
                }

                if (paragraphBuilder != nint.Zero)
                {
                    NativeMethods.ImpellerParagraphBuilderRelease(paragraphBuilder);
                }

                if (paragraphStyle != nint.Zero)
                {
                    NativeMethods.ImpellerParagraphStyleRelease(paragraphStyle);
                }
            }
        }

    // ─── Paths ──────────────────────────────────────────────────────────

    public void DrawBezier(float x1, float y1, float cx1, float cy1,
                           float cx2, float cy2, float x2, float y2, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        var paintHandle = GetStrokePaint(color, lineWidth);
        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        nint path = nint.Zero;
        try
        {
            var p0 = new ImpellerPoint(x1, y1);
            NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
            var cp1 = new ImpellerPoint(cx1, cy1);
            var cp2 = new ImpellerPoint(cx2, cy2);
            var end = new ImpellerPoint(x2, y2);
            NativeMethods.ImpellerPathBuilderCubicCurveTo(pathBuilder, ref cp1, ref cp2, ref end);
            path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
        }
        finally
        {
            if (path != nint.Zero)
            {
                NativeMethods.ImpellerPathRelease(path);
            }

            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
    }

    public void DrawPath(nint pathHandle, Color color, float lineWidth)
    {
        if (_builder is null)
            return;
        _builder.DrawPath(pathHandle, GetStrokePaint(color, lineWidth));
    }

    public void FillPath(nint pathHandle, Color color)
    {
        if (_builder is null)
            return;
        _builder.DrawPath(pathHandle, GetFillPaint(color));
    }

    internal void FillRectPath(List<RectangleF> rectangles, Color color)
    {
        if (_builder is null || rectangles.Count == 0)
            return;

        var paintHandle = GetFillPaint(color);
        var pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        try
        {
            foreach (RectangleF rectangle in rectangles)
            {
                if (rectangle.Width <= 0 || rectangle.Height <= 0)
                    continue;

                var p0 = new ImpellerPoint(rectangle.Left, rectangle.Top);
                var p1 = new ImpellerPoint(rectangle.Right, rectangle.Top);
                var p2 = new ImpellerPoint(rectangle.Right, rectangle.Bottom);
                var p3 = new ImpellerPoint(rectangle.Left, rectangle.Bottom);
                NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref p0);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p1);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p2);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref p3);
                NativeMethods.ImpellerPathBuilderClose(pathBuilder);
            }

            var path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, 0);
            _builder.DrawPath(path, paintHandle);
            NativeMethods.ImpellerPathRelease(path);
        }
        finally
        {
            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
    }

    // ─── Images ─────────────────────────────────────────────────────────

    public void DrawImage(Image image, float x, float y)
    {
        WinFormsXCompatibilityWarning.Once(
            "ImpellerRenderingBackend.DrawImage",
            "Graphics.DrawImage is not wired to Impeller image textures yet; the image draw command was ignored.");
        // if (_builder is null) return;
        // var texture = image.GetTextureHandle(_impellerContext);
        // if (texture == nint.Zero) return;
        // var point = new ImpellerPoint(x, y);
        // _builder.DrawTexture(texture, ref point, nint.Zero);
    }

    public void DrawImageRect(Image image, float sx, float sy, float sw, float sh,
                              float dx, float dy, float dw, float dh)
    {
        WinFormsXCompatibilityWarning.Once(
            "ImpellerRenderingBackend.DrawImageRect",
            "Graphics.DrawImage with source/destination rectangles is not wired to Impeller image textures yet; the image draw command was ignored.");
        // if (_builder is null) return;
        // var texture = image.GetTextureHandle(_impellerContext);
        // if (texture == nint.Zero) return;
        // var src = new ImpellerRect(sx, sy, sw, sh);
        // var dst = new ImpellerRect(dx, dy, dw, dh);
        // _builder.DrawTextureRect(texture, ref src, ref dst, nint.Zero);
    }

    // ─── Helpers ────────────────────────────────────────────────────────

    private nint GetFillPaint(Color color)
        => GetOrCreatePaint(new PaintKey(color.ToArgb(), PaintKind.Fill, 0), color, 0f);

    private nint GetStrokePaint(Color color, float lineWidth)
        => GetOrCreatePaint(new PaintKey(color.ToArgb(), PaintKind.Stroke, BitConverter.SingleToInt32Bits(lineWidth)), color, lineWidth);

    private nint GetOrCreatePaint(PaintKey key, Color color, float lineWidth)
    {
        if (_framePaintCache.TryGetValue(key, out nint existing))
        {
            return existing;
        }

        var handle = NativeMethods.ImpellerPaintNew();
        var ic = color.ToImpellerColor();
        NativeMethods.ImpellerPaintSetColor(handle, ref ic);
        NativeMethods.ImpellerPaintSetDrawStyle(handle, key.Kind == PaintKind.Fill ? ImpellerDrawStyle.Fill : ImpellerDrawStyle.Stroke);
        if (key.Kind == PaintKind.Stroke)
        {
            NativeMethods.ImpellerPaintSetStrokeWidth(handle, lineWidth);
        }

        _framePaintCache[key] = handle;
        return handle;
        }

    private void ReleaseFrameParagraphs()
    {
        foreach (nint paragraph in _frameParagraphs)
        {
            NativeMethods.ImpellerParagraphRelease(paragraph);
        }

        _frameParagraphs.Clear();
    }

    private void ReleaseFramePaints()
    {
        foreach (nint paint in _framePaintCache.Values)
        {
            NativeMethods.ImpellerPaintRelease(paint);
        }

        _framePaintCache.Clear();
    }
}
#endif

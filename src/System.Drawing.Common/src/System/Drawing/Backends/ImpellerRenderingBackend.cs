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

    internal void FillGlyphOutline(TrueTypeGlyphOutline outline, float x, float baselineY, float scale, Color color)
    {
        if (_builder is null || outline.IsEmpty || scale <= 0f)
            return;

        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        nint path = nint.Zero;
        try
        {
            foreach (TrueTypeGlyphPoint[] contour in outline.Contours)
            {
                AppendGlyphContour(pathBuilder, contour, x, baselineY, scale);
            }

            path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, ImpellerFillType.NonZero);
            if (path != nint.Zero)
            {
                _builder.DrawPath(path, GetFillPaint(color));
            }
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

    private static void AppendGlyphContour(nint pathBuilder, TrueTypeGlyphPoint[] contour, float x, float baselineY, float scale)
    {
        if (contour.Length == 0)
        {
            return;
        }

        int startIndex = ResolveContourStartIndex(contour);
        TrueTypeGlyphPoint startPoint = ResolveContourStartPoint(contour, startIndex);
        var start = ToImpellerPoint(startPoint, x, baselineY, scale);
        NativeMethods.ImpellerPathBuilderMoveTo(pathBuilder, ref start);

        int index = (startIndex + 1) % contour.Length;
        int processed = 0;
        while (processed < contour.Length)
        {
            TrueTypeGlyphPoint point = contour[index];
            if (point.OnCurve)
            {
                var to = ToImpellerPoint(point, x, baselineY, scale);
                NativeMethods.ImpellerPathBuilderLineTo(pathBuilder, ref to);
                index = (index + 1) % contour.Length;
                processed++;
                continue;
            }

            int nextIndex = (index + 1) % contour.Length;
            TrueTypeGlyphPoint next = contour[nextIndex];
            TrueTypeGlyphPoint endPoint;
            int consumed;
            if (next.OnCurve)
            {
                endPoint = next;
                consumed = 2;
            }
            else
            {
                endPoint = Midpoint(point, next);
                consumed = 1;
            }

            var control = ToImpellerPoint(point, x, baselineY, scale);
            var end = ToImpellerPoint(endPoint, x, baselineY, scale);
            NativeMethods.ImpellerPathBuilderQuadraticCurveTo(pathBuilder, ref control, ref end);
            index = (index + consumed) % contour.Length;
            processed += consumed;
        }

        NativeMethods.ImpellerPathBuilderClose(pathBuilder);
    }

    private static int ResolveContourStartIndex(TrueTypeGlyphPoint[] contour)
    {
        if (contour[0].OnCurve)
        {
            return 0;
        }

        return contour[^1].OnCurve ? contour.Length - 1 : 0;
    }

    private static TrueTypeGlyphPoint ResolveContourStartPoint(TrueTypeGlyphPoint[] contour, int startIndex)
    {
        TrueTypeGlyphPoint start = contour[startIndex];
        if (start.OnCurve)
        {
            return start;
        }

        return Midpoint(contour[^1], contour[0]);
    }

    private static TrueTypeGlyphPoint Midpoint(TrueTypeGlyphPoint first, TrueTypeGlyphPoint second) =>
        new((first.X + second.X) / 2f, (first.Y + second.Y) / 2f, OnCurve: true);

    private static ImpellerPoint ToImpellerPoint(TrueTypeGlyphPoint point, float x, float baselineY, float scale) =>
        new(x + (point.X * scale), baselineY - (point.Y * scale));

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

// Impeller rendering backend — records drawing commands via DisplayListBuilder
// and renders them to the GPU via Impeller's Vulkan/Metal surface.

#if !BROWSER

using System.Drawing.Impeller;
using System.Text;
using System.Threading;

namespace System.Drawing;

/// <summary>
/// Desktop rendering backend. Uses Impeller's DisplayListBuilder for GPU-accelerated
/// rendering via Vulkan or Metal.
/// </summary>
internal sealed class ImpellerRenderingBackend : IRenderingBackend, IDisposable
    {
        private enum PaintKind
        {
            Fill,
            Stroke,
        }

        private enum TextDiagnosticMode
        {
            ManagedGlyphs,
            NativeParagraph,
            None,
            StaticParagraph,
        }

        private readonly record struct PaintKey(int Argb, PaintKind Kind, int StrokeWidthBits);
        private readonly record struct ParagraphKey(
            string Text,
            string FontFamily,
            int FontSizeBits,
            int Argb,
            bool Bold,
            bool Italic);

        private readonly record struct TransformSnapshot(float ScaleX, float ScaleY, float OffsetX, float OffsetY);

        private const int MaxGlyphsPerPath = 64;
        private const int DefaultMaxParagraphCacheEntries = 512;
        private const int DefaultMaxDeferredFrames = 4;
        private const int DefaultMaxTextDrawsPerFrame = 0;

        private readonly IPlatformBackend _platformBackend;
        private readonly nint _impellerContext;
        private static readonly HarfBuzzTextEngine s_textEngine = new();
        private static readonly string? s_traceFile = Environment.GetEnvironmentVariable("WINFORMSX_TRACE_FILE");
        private static readonly TextDiagnosticMode s_textDiagnosticMode = ResolveTextDiagnosticMode();
        private static readonly int s_maxParagraphCacheEntries = ResolvePositiveInt(
            "WINFORMSX_IMPELLER_MAX_PARAGRAPH_CACHE",
            DefaultMaxParagraphCacheEntries);
        private static readonly int s_maxDeferredFrames = ResolvePositiveInt(
            "WINFORMSX_IMPELLER_DEFERRED_FRAMES",
            DefaultMaxDeferredFrames);
        private static readonly int s_maxTextDrawsPerFrame = ResolveNonNegativeInt(
            "WINFORMSX_IMPELLER_MAX_TEXT_DRAWS",
            DefaultMaxTextDrawsPerFrame);
        private static long s_nextFrameId;
        private readonly Dictionary<PaintKey, nint> _framePaintCache = [];
        private readonly Dictionary<ParagraphKey, NativeParagraphEntry> _paragraphCache = [];
        private readonly Queue<ParagraphKey> _paragraphCacheOrder = [];
        private readonly Queue<DeferredFrameResources> _deferredFrameResources = [];
        private readonly Dictionary<int, List<PositionedGlyphOutline>> _frameGlyphBatches = [];
        private readonly Stack<TransformSnapshot> _transformStack = [];
        private DisplayListBuilder? _builder;
        private FrameCounters _frameCounters;
        private nint _frameSurface;
        private long _frameId;
        private float _scaleX = 1f;
        private float _scaleY = 1f;
        private float _offsetX;
        private float _offsetY;
        private bool _staticDiagnosticParagraphDrawn;
        private bool _disposed;

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
        if (_disposed)
        {
            return false;
        }

        _frameId = Interlocked.Increment(ref s_nextFrameId);
        _frameCounters = new FrameCounters();
        _staticDiagnosticParagraphDrawn = false;
        _frameSurface = _platformBackend.AcquireNextSurface();
        if (_frameSurface == nint.Zero)
        {
            Trace($"[ImpellerFrame:{_frameId}] Acquire surface failed size={width}x{height}");
            return false;
        }

        _framePaintCache.Clear();
        _frameGlyphBatches.Clear();
        ResetTrackedTransform();
        _builder = width > 0 && height > 0
            ? new DisplayListBuilder(width, height)
            : new DisplayListBuilder();
        Trace($"[ImpellerFrame:{_frameId}] Begin size={width}x{height} surface=0x{_frameSurface:X} textMode={s_textDiagnosticMode} paragraphCache={_paragraphCache.Count}");
        return true;
    }

    public void EndFrame(int width, int height)
    {
        if (_disposed || _builder is null)
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
        bool presented = false;
        try
        {
            FlushGlyphBatches();
            displayList = _builder.Build();
            Trace($"[ImpellerFrame:{_frameId}] Build displayList=0x{displayList:X} {FormatFrameCounters()}");
            bool drawSucceeded = NativeMethods.ImpellerSurfaceDrawDisplayList(_frameSurface, displayList);
            Trace($"[ImpellerFrame:{_frameId}] DrawDisplayList success={drawSucceeded} surface=0x{_frameSurface:X} displayList=0x{displayList:X}");
            if (!drawSucceeded)
            {
                throw new InvalidOperationException("Impeller failed to draw the display list.");
            }

            bool presentSucceeded = _platformBackend.PresentSurface(_frameSurface);
            Trace($"[ImpellerFrame:{_frameId}] Present success={presentSucceeded} surface=0x{_frameSurface:X}");
            if (!presentSucceeded)
            {
                throw new InvalidOperationException("Impeller failed to present the frame surface.");
            }

            presented = true;
        }
        finally
        {
            if (presented)
            {
                EnqueueDeferredFrameResources(displayList, DetachFramePaints());
                displayList = nint.Zero;
            }
            else
            {
                if (displayList != nint.Zero)
                {
                    NativeMethods.ImpellerDisplayListRelease(displayList);
                    Trace($"[ImpellerFrame:{_frameId}] Release displayList=0x{displayList:X}");
                }

                ReleaseFramePaints();
            }

            Trace($"[ImpellerFrame:{_frameId}] Release surface=0x{_frameSurface:X}");
            NativeMethods.ImpellerSurfaceRelease(_frameSurface);
            _frameSurface = nint.Zero;
            _frameGlyphBatches.Clear();
            ResetTrackedTransform();
            _builder.Dispose();
            _builder = null;
        }
    }

    public void AbortFrame()
    {
        if (_frameSurface != nint.Zero)
        {
            Trace($"[ImpellerFrame:{_frameId}] Abort release surface=0x{_frameSurface:X} {FormatFrameCounters()}");
            NativeMethods.ImpellerSurfaceRelease(_frameSurface);
            _frameSurface = nint.Zero;
        }

        ReleaseFramePaints();
        _frameGlyphBatches.Clear();
        ResetTrackedTransform();
        _builder?.Dispose();
        _builder = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AbortFrame();
        ReleaseDeferredFrameResources(0);
        foreach (NativeParagraphEntry entry in _paragraphCache.Values)
        {
            entry.Dispose();
        }

        _paragraphCache.Clear();
        _paragraphCacheOrder.Clear();
    }

    public void FlushPending()
    {
        FlushGlyphBatches();
    }

    // ─── State Stack ────────────────────────────────────────────────────

    public void Save()
    {
        FlushGlyphBatches();
        _transformStack.Push(new TransformSnapshot(_scaleX, _scaleY, _offsetX, _offsetY));
        _frameCounters.Saves++;
        _builder?.Save();
    }

    public void Restore()
    {
        FlushGlyphBatches();
        _frameCounters.Restores++;
        _builder?.Restore();
        if (_transformStack.TryPop(out TransformSnapshot transform))
        {
            ApplyTrackedTransform(transform);
        }
    }

    public uint SaveCount => _builder?.SaveCount ?? 0;
    public void RestoreToCount(uint count)
    {
        FlushGlyphBatches();
        _frameCounters.Restores++;
        _builder?.RestoreToCount(count);
        while (_transformStack.Count > count)
        {
            _transformStack.Pop();
        }

        if (_transformStack.TryPeek(out TransformSnapshot transform))
        {
            ApplyTrackedTransform(transform);
        }
        else
        {
            ResetTrackedTransform();
        }
    }

    // ─── Transforms ─────────────────────────────────────────────────────

    public void Translate(float dx, float dy)
    {
        FlushGlyphBatches();
        _offsetX += dx * _scaleX;
        _offsetY += dy * _scaleY;
        _builder?.Translate(dx, dy);
    }

    public void Scale(float sx, float sy)
    {
        FlushGlyphBatches();
        _scaleX *= sx;
        _scaleY *= sy;
        _offsetX *= sx;
        _offsetY *= sy;
        _builder?.Scale(sx, sy);
    }

    public void Rotate(float radians)
    {
        FlushGlyphBatches();
        _builder?.Rotate(radians * 180f / MathF.PI); // Impeller uses degrees
    }

    public void ResetTransform()
    {
        FlushGlyphBatches();
        _scaleX = 1f;
        _scaleY = 1f;
        _offsetX = 0f;
        _offsetY = 0f;
        _builder?.ResetTransform();
    }

    // ─── Clipping ───────────────────────────────────────────────────────

    public void ClipRect(float x, float y, float w, float h)
    {
        if (!PrepareNonTextDraw())
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _frameCounters.Clips++;
        _builder.ClipRect(ref rect);
    }

    // ─── Fill Operations ────────────────────────────────────────────────

    public void Clear(Color color)
    {
        if (!PrepareNonTextDraw())
            return;
        _frameCounters.DrawPaints++;
        _builder.DrawPaint(GetFillPaint(color));
    }

    public void FillRect(float x, float y, float w, float h, Color color)
    {
        if (!PrepareNonTextDraw())
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _frameCounters.Rects++;
        _builder.DrawRect(ref rect, GetFillPaint(color));
    }

    public void FillEllipse(float x, float y, float w, float h, Color color)
    {
        if (!PrepareNonTextDraw())
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _frameCounters.Ovals++;
        _builder.DrawOval(ref rect, GetFillPaint(color));
    }

    public void FillPolygon(Point[] points, Color color)
    {
        if (!PrepareNonTextDraw() || points.Length < 3)
            return;
        var paintHandle = GetFillPaint(color);
        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        _frameCounters.PathBuilders++;
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
            _frameCounters.Paths++;
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
        if (!PrepareNonTextDraw())
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _frameCounters.Rects++;
        _builder.DrawRect(ref rect, GetStrokePaint(color, lineWidth));
    }

    public void StrokeEllipse(float x, float y, float w, float h, Color color, float lineWidth)
    {
        if (!PrepareNonTextDraw())
            return;
        var rect = new ImpellerRect(x, y, w, h);
        _frameCounters.Ovals++;
        _builder.DrawOval(ref rect, GetStrokePaint(color, lineWidth));
    }

    public void StrokeLine(float x1, float y1, float x2, float y2, Color color, float lineWidth)
    {
        if (!PrepareNonTextDraw())
            return;
        var from = new ImpellerPoint(x1, y1);
        var to = new ImpellerPoint(x2, y2);
        _frameCounters.Lines++;
        _builder.DrawLine(ref from, ref to, GetStrokePaint(color, lineWidth));
    }

    public void StrokePolygon(Point[] points, Color color, float lineWidth)
    {
        if (!PrepareNonTextDraw() || points.Length < 3)
            return;
        var paintHandle = GetStrokePaint(color, lineWidth);
        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        _frameCounters.PathBuilders++;
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
            _frameCounters.Paths++;
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
        if (s_textDiagnosticMode == TextDiagnosticMode.ManagedGlyphs)
        {
            DrawManagedGlyphText(text, x, y, color, fontFamily, fontSize, bold, italic);
            return;
        }

        DrawNativeParagraphText(text, x, y, color, fontFamily, fontSize, bold, italic);
    }

    public void DrawStringAligned(string text, RectangleF bounds, ContentAlignment alignment,
                                  Color color, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (s_textDiagnosticMode == TextDiagnosticMode.ManagedGlyphs)
        {
            DrawManagedGlyphTextAligned(text, bounds, alignment, color, fontFamily, fontSize, bold, italic);
            return;
        }

        SizeF measured = MeasureString(text, fontFamily, fontSize, bold, italic);
        float x = bounds.X;
        float y = bounds.Y;

        if (alignment is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter)
        {
            x += MathF.Max(0f, (bounds.Width - measured.Width) / 2f);
        }
        else if (alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight)
        {
            x += MathF.Max(0f, bounds.Width - measured.Width);
        }

        if (alignment is ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight)
        {
            y += MathF.Max(0f, (bounds.Height - measured.Height) / 2f);
        }
        else if (alignment is ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight)
        {
            y += MathF.Max(0f, bounds.Height - measured.Height);
        }

        DrawNativeParagraphText(text, x, y, color, fontFamily, fontSize, bold, italic);
    }

    public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic)
    {
        return s_textEngine.MeasureString(text, fontFamily, fontSize, bold, italic);
    }

    private void DrawNativeParagraphText(
        string text,
        float x,
        float y,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        if (string.IsNullOrEmpty(text) || !PrepareNonTextDraw())
        {
            return;
        }

        _frameCounters.TextRequests++;
        if (s_textDiagnosticMode == TextDiagnosticMode.None)
        {
            _frameCounters.TextSkipped++;
            return;
        }

        if (s_textDiagnosticMode == TextDiagnosticMode.StaticParagraph)
        {
            if (_staticDiagnosticParagraphDrawn)
            {
                _frameCounters.TextSkipped++;
                return;
            }

            text = "Impeller diagnostic paragraph";
            _staticDiagnosticParagraphDrawn = true;
        }

        string normalized = SanitizeNativeText(text).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        float lineHeight = MathF.Max(1f, MeasureString("Ag", fontFamily, fontSize, bold, italic).Height);
        string? resolvedFamily = TypographyProvider.ResolveFontFamily(fontFamily, bold, italic);
        if (string.IsNullOrWhiteSpace(resolvedFamily))
        {
            WinFormsXCompatibilityWarning.Once(
                "ImpellerRenderingBackend.NativeText.NoFont",
                $"No Impeller font could be resolved for '{fontFamily}'; the text draw command was ignored.");
            return;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            DrawNativeParagraphLine(
                line,
                x,
                y + (i * lineHeight),
                color,
                resolvedFamily,
                fontSize,
                bold,
                italic);
        }
    }

    private void DrawManagedGlyphText(
        string text,
        float x,
        float y,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        if (string.IsNullOrEmpty(text) || _builder is null)
        {
            return;
        }

        _frameCounters.TextRequests++;
        if (TextBudgetExhausted())
        {
            _frameCounters.TextSkipped++;
            return;
        }

        try
        {
            s_textEngine.DrawString(this, text, x, y, color, fontFamily, fontSize, bold, italic);
            _frameCounters.TextDrawn++;
        }
        catch (InvalidOperationException ex)
        {
            _frameCounters.TextSkipped++;
            WinFormsXCompatibilityWarning.Once(
                "ImpellerRenderingBackend.ManagedGlyphText.NoFont",
                $"No managed glyph font could be resolved for '{fontFamily}'; the text draw command was ignored. {ex.Message}");
        }
    }

    private void DrawManagedGlyphTextAligned(
        string text,
        RectangleF bounds,
        ContentAlignment alignment,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        if (string.IsNullOrEmpty(text) || _builder is null)
        {
            return;
        }

        _frameCounters.TextRequests++;
        if (TextBudgetExhausted())
        {
            _frameCounters.TextSkipped++;
            return;
        }

        try
        {
            s_textEngine.DrawStringAligned(this, text, bounds, alignment, color, fontFamily, fontSize, bold, italic);
            _frameCounters.TextDrawn++;
        }
        catch (InvalidOperationException ex)
        {
            _frameCounters.TextSkipped++;
            WinFormsXCompatibilityWarning.Once(
                "ImpellerRenderingBackend.ManagedGlyphText.NoFont",
                $"No managed glyph font could be resolved for '{fontFamily}'; the text draw command was ignored. {ex.Message}");
        }
    }

    private void DrawNativeParagraphLine(
        string line,
        float x,
        float y,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        nint typography = TypographyProvider.Context;
        if (typography == nint.Zero || _builder is null)
        {
            return;
        }

        if (TextBudgetExhausted())
        {
            _frameCounters.TextSkipped++;
            return;
        }

        NativeParagraphEntry? paragraphEntry = GetOrCreateParagraph(
            line,
            fontFamily,
            fontSize,
            color,
            bold,
            italic,
            typography);
        if (paragraphEntry is null)
        {
            return;
        }

        var point = new ImpellerPoint(x, y);
        _frameCounters.ParagraphDrawn++;
        _frameCounters.TextDrawn++;
        _builder.DrawParagraph(paragraphEntry.Paragraph, ref point);
    }

    private NativeParagraphEntry? GetOrCreateParagraph(
        string line,
        string fontFamily,
        float fontSize,
        Color color,
        bool bold,
        bool italic,
        nint typography)
    {
        var key = new ParagraphKey(
            line,
            fontFamily,
            BitConverter.SingleToInt32Bits(MathF.Max(1f, fontSize)),
            color.ToArgb(),
            bold,
            italic);

        if (_paragraphCache.TryGetValue(key, out NativeParagraphEntry? cached))
        {
            _frameCounters.ParagraphReused++;
            return cached;
        }

        nint paragraphStyle = NativeMethods.ImpellerParagraphStyleNew();
        if (paragraphStyle == nint.Zero)
        {
            return null;
        }

        nint paint = nint.Zero;
        nint paragraphBuilder = nint.Zero;
        nint paragraph = nint.Zero;
        try
        {
            paint = NativeMethods.ImpellerPaintNew();
            if (paint == nint.Zero)
            {
                return null;
            }

            var ic = color.ToImpellerColor();
            NativeMethods.ImpellerPaintSetColor(paint, ref ic);
            NativeMethods.ImpellerPaintSetDrawStyle(paint, ImpellerDrawStyle.Fill);
            NativeMethods.ImpellerParagraphStyleSetForeground(paragraphStyle, paint);

            byte[] familyUtf8 = Encoding.UTF8.GetBytes(fontFamily + '\0');
            unsafe
            {
                fixed (byte* pFamily = familyUtf8)
                {
                    NativeMethods.ImpellerParagraphStyleSetFontFamily(paragraphStyle, pFamily);
                }
            }

            NativeMethods.ImpellerParagraphStyleSetFontSize(paragraphStyle, MathF.Max(1f, fontSize));
            NativeMethods.ImpellerParagraphStyleSetFontWeight(
                paragraphStyle,
                bold ? ImpellerFontWeight.Bold : ImpellerFontWeight.Regular);
            NativeMethods.ImpellerParagraphStyleSetFontStyle(
                paragraphStyle,
                italic ? ImpellerFontStyle.Italic : ImpellerFontStyle.Normal);
            NativeMethods.ImpellerParagraphStyleSetTextDirection(paragraphStyle, ImpellerTextDirection.LeftToRight);
            NativeMethods.ImpellerParagraphStyleSetTextAlignment(paragraphStyle, ImpellerTextAlignment.Left);

            paragraphBuilder = NativeMethods.ImpellerParagraphBuilderNew(typography);
            if (paragraphBuilder == nint.Zero)
            {
                return null;
            }

            NativeMethods.ImpellerParagraphBuilderPushStyle(paragraphBuilder, paragraphStyle);
            byte[] utf8 = Encoding.UTF8.GetBytes(line);
            unsafe
            {
                fixed (byte* pText = utf8)
                {
                    NativeMethods.ImpellerParagraphBuilderAddText(paragraphBuilder, pText, (uint)utf8.Length);
                }
            }

            NativeMethods.ImpellerParagraphBuilderPopStyle(paragraphBuilder);
            paragraph = NativeMethods.ImpellerParagraphBuilderBuildParagraphNew(paragraphBuilder, 100_000f);
            if (paragraph == nint.Zero)
            {
                return null;
            }

            var entry = new NativeParagraphEntry(paragraph, paint);
            paragraph = nint.Zero;
            paint = nint.Zero;
            _paragraphCache[key] = entry;
            _paragraphCacheOrder.Enqueue(key);
            _frameCounters.ParagraphCreated++;
            TrimParagraphCache();
            return entry;
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

            if (paint != nint.Zero)
            {
                NativeMethods.ImpellerPaintRelease(paint);
            }

            NativeMethods.ImpellerParagraphStyleRelease(paragraphStyle);
        }
    }

    private static string SanitizeNativeText(string text)
    {
        char[] chars = text.ToCharArray();
        bool changed = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is '\r' or '\n' or '\t')
            {
                continue;
            }

            if (c < 32 || c > 126)
            {
                chars[i] = '?';
                changed = true;
            }
        }

        return changed ? new string(chars) : text;
    }

    // ─── Paths ──────────────────────────────────────────────────────────

    public void DrawBezier(float x1, float y1, float cx1, float cy1,
                           float cx2, float cy2, float x2, float y2, Color color, float lineWidth)
    {
        if (!PrepareNonTextDraw())
            return;
        var paintHandle = GetStrokePaint(color, lineWidth);
        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        _frameCounters.PathBuilders++;
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
            _frameCounters.Paths++;
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
        if (!PrepareNonTextDraw())
            return;
        _frameCounters.Paths++;
        _builder.DrawPath(pathHandle, GetStrokePaint(color, lineWidth));
    }

    public void FillPath(nint pathHandle, Color color)
    {
        if (!PrepareNonTextDraw())
            return;
        _frameCounters.Paths++;
        _builder.DrawPath(pathHandle, GetFillPaint(color));
    }

    internal void FillRectPath(List<RectangleF> rectangles, Color color)
    {
        if (!PrepareNonTextDraw() || rectangles.Count == 0)
            return;

        var paintHandle = GetFillPaint(color);
        var pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        _frameCounters.PathBuilders++;
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
            _frameCounters.Paths++;
            _builder.DrawPath(path, paintHandle);
            NativeMethods.ImpellerPathRelease(path);
        }
        finally
        {
            NativeMethods.ImpellerPathBuilderRelease(pathBuilder);
        }
    }

    internal void FillGlyphOutlines(IReadOnlyList<PositionedGlyphOutline> glyphs, Color color)
    {
        FillGlyphOutlines(glyphs, 0, glyphs.Count, color);
    }

    private void FillGlyphOutlines(IReadOnlyList<PositionedGlyphOutline> glyphs, int start, int count, Color color)
    {
        if (_builder is null || glyphs.Count == 0)
            return;

        nint pathBuilder = NativeMethods.ImpellerPathBuilderNew();
        _frameCounters.PathBuilders++;
        nint path = nint.Zero;
        try
        {
            int end = Math.Min(glyphs.Count, start + count);
            for (int i = start; i < end; i++)
            {
                PositionedGlyphOutline glyph = glyphs[i];
                if (glyph.Outline.IsEmpty || glyph.Scale <= 0f)
                {
                    continue;
                }

                foreach (TrueTypeGlyphPoint[] contour in glyph.Outline.Contours)
                {
                    AppendGlyphContour(pathBuilder, contour, glyph.X, glyph.BaselineY, glyph.Scale);
                }
            }

            path = NativeMethods.ImpellerPathBuilderCopyPathNew(pathBuilder, ImpellerFillType.NonZero);
            if (path != nint.Zero)
            {
                _frameCounters.Paths++;
                _builder.Save();
                _frameCounters.Saves++;
                _builder.ResetTransform();
                _builder.DrawPath(path, GetFillPaint(color));
                _builder.Restore();
                _frameCounters.Restores++;
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

    internal void EnqueueGlyphOutlines(IReadOnlyList<PositionedGlyphOutline> glyphs, Color color)
    {
        if (_builder is null || glyphs.Count == 0)
            return;

        int key = color.ToArgb();
        if (!_frameGlyphBatches.TryGetValue(key, out List<PositionedGlyphOutline>? batch))
        {
            batch = [];
            _frameGlyphBatches[key] = batch;
        }

        foreach (PositionedGlyphOutline glyph in glyphs)
        {
            _frameCounters.GlyphsEnqueued++;
            batch.Add(glyph with
            {
                X = glyph.X * _scaleX + _offsetX,
                BaselineY = glyph.BaselineY * _scaleY + _offsetY,
                Scale = glyph.Scale * _scaleX
            });
        }
    }

    private void FlushGlyphBatches()
    {
        if (_frameGlyphBatches.Count == 0)
            return;

        foreach ((int argb, List<PositionedGlyphOutline> glyphs) in _frameGlyphBatches)
        {
            Color color = Color.FromArgb(argb);
            for (int i = 0; i < glyphs.Count; i += MaxGlyphsPerPath)
            {
                int count = Math.Min(MaxGlyphsPerPath, glyphs.Count - i);
                FillGlyphOutlines(glyphs, i, count, color);
            }
        }

        _frameGlyphBatches.Clear();
    }

    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(_builder))]
    private bool PrepareNonTextDraw()
    {
        if (_builder is null)
        {
            return false;
        }

        FlushGlyphBatches();
        return true;
    }

    private void ResetTrackedTransform()
    {
        _transformStack.Clear();
        _scaleX = 1f;
        _scaleY = 1f;
        _offsetX = 0f;
        _offsetY = 0f;
    }

    private void ApplyTrackedTransform(TransformSnapshot transform)
    {
        _scaleX = transform.ScaleX;
        _scaleY = transform.ScaleY;
        _offsetX = transform.OffsetX;
        _offsetY = transform.OffsetY;
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
            _frameCounters.PaintsReused++;
            return existing;
        }

        var handle = NativeMethods.ImpellerPaintNew();
        _frameCounters.PaintsCreated++;
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
        List<nint> paints = DetachFramePaints();
        foreach (nint paint in paints)
        {
            NativeMethods.ImpellerPaintRelease(paint);
        }

        if (paints.Count > 0)
        {
            Trace($"[ImpellerFrame:{_frameId}] Release framePaints={paints.Count}");
        }
    }

    private List<nint> DetachFramePaints()
    {
        var paints = new List<nint>(_framePaintCache.Values);
        _framePaintCache.Clear();
        return paints;
    }

    private void EnqueueDeferredFrameResources(nint displayList, List<nint> paints)
    {
        _deferredFrameResources.Enqueue(new DeferredFrameResources(_frameId, displayList, paints));
        Trace($"[ImpellerFrame:{_frameId}] Defer displayList=0x{displayList:X} framePaints={paints.Count} deferredFrames={_deferredFrameResources.Count}");
        ReleaseDeferredFrameResources(s_maxDeferredFrames);
    }

    private void ReleaseDeferredFrameResources(int retainFrames)
    {
        while (_deferredFrameResources.Count > retainFrames)
        {
            DeferredFrameResources resources = _deferredFrameResources.Dequeue();
            if (resources.DisplayList != nint.Zero)
            {
                NativeMethods.ImpellerDisplayListRelease(resources.DisplayList);
            }

            foreach (nint paint in resources.Paints)
            {
                NativeMethods.ImpellerPaintRelease(paint);
            }

            Trace($"[ImpellerFrame:{_frameId}] Retire deferredFrame={resources.FrameId} displayList=0x{resources.DisplayList:X} framePaints={resources.Paints.Count} remainingDeferredFrames={_deferredFrameResources.Count}");
        }
    }

    private void TrimParagraphCache()
    {
        while (_paragraphCache.Count > s_maxParagraphCacheEntries && _paragraphCacheOrder.TryDequeue(out ParagraphKey oldest))
        {
            if (_paragraphCache.Remove(oldest, out NativeParagraphEntry? entry))
            {
                _frameCounters.ParagraphEvicted++;
                entry.Dispose();
            }
        }
    }

    private bool TextBudgetExhausted()
        => s_maxTextDrawsPerFrame > 0 && _frameCounters.TextDrawn >= s_maxTextDrawsPerFrame;

    private string FormatFrameCounters()
        => $"ops paint={_frameCounters.DrawPaints} rect={_frameCounters.Rects} oval={_frameCounters.Ovals} line={_frameCounters.Lines} path={_frameCounters.Paths} pathBuilder={_frameCounters.PathBuilders} clip={_frameCounters.Clips} save={_frameCounters.Saves} restore={_frameCounters.Restores} textReq={_frameCounters.TextRequests} textDraw={_frameCounters.TextDrawn} textSkip={_frameCounters.TextSkipped} textBudget={FormatTextBudget()} paraDraw={_frameCounters.ParagraphDrawn} paraCreate={_frameCounters.ParagraphCreated} paraReuse={_frameCounters.ParagraphReused} paraEvict={_frameCounters.ParagraphEvicted} glyphs={_frameCounters.GlyphsEnqueued} paintsCreate={_frameCounters.PaintsCreated} paintsReuse={_frameCounters.PaintsReused} paragraphCache={_paragraphCache.Count}";

    private static string FormatTextBudget()
        => s_maxTextDrawsPerFrame > 0 ? s_maxTextDrawsPerFrame.ToString() : "unlimited";

    private static TextDiagnosticMode ResolveTextDiagnosticMode()
        => Environment.GetEnvironmentVariable("WINFORMSX_IMPELLER_TEXT_MODE")?.Trim().ToLowerInvariant() switch
        {
            "none" or "off" or "0" => TextDiagnosticMode.None,
            "static" or "static-paragraph" or "one" => TextDiagnosticMode.StaticParagraph,
            "native" or "paragraph" or "native-paragraph" => TextDiagnosticMode.NativeParagraph,
            "managed" or "glyphs" or "harfbuzz" => TextDiagnosticMode.ManagedGlyphs,
            _ => TextDiagnosticMode.NativeParagraph,
        };

    private static int ResolvePositiveInt(string variableName, int defaultValue)
        => int.TryParse(Environment.GetEnvironmentVariable(variableName), out int value) && value > 0
            ? value
            : defaultValue;

    private static int ResolveNonNegativeInt(string variableName, int defaultValue)
        => int.TryParse(Environment.GetEnvironmentVariable(variableName), out int value) && value >= 0
            ? value
            : defaultValue;

    private static void Trace(string message)
    {
        if (string.IsNullOrWhiteSpace(s_traceFile))
        {
            return;
        }

        try
        {
            System.IO.File.AppendAllText(s_traceFile, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private struct FrameCounters
    {
        public int DrawPaints;
        public int Rects;
        public int Ovals;
        public int Lines;
        public int Paths;
        public int PathBuilders;
        public int Clips;
        public int Saves;
        public int Restores;
        public int TextRequests;
        public int TextDrawn;
        public int TextSkipped;
        public int ParagraphDrawn;
        public int ParagraphCreated;
        public int ParagraphReused;
        public int ParagraphEvicted;
        public int GlyphsEnqueued;
        public int PaintsCreated;
        public int PaintsReused;
    }

    private sealed record DeferredFrameResources(long FrameId, nint DisplayList, List<nint> Paints);

    private sealed class NativeParagraphEntry : IDisposable
    {
        public NativeParagraphEntry(nint paragraph, nint paint)
        {
            Paragraph = paragraph;
            Paint = paint;
        }

        public nint Paragraph { get; private set; }

        private nint Paint { get; set; }

        public void Dispose()
        {
            if (Paragraph != nint.Zero)
            {
                NativeMethods.ImpellerParagraphRelease(Paragraph);
                Paragraph = nint.Zero;
            }

            if (Paint != nint.Zero)
            {
                NativeMethods.ImpellerPaintRelease(Paint);
                Paint = nint.Zero;
            }
        }
    }
}
#endif

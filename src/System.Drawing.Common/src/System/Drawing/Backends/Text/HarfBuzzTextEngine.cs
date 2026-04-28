using System.Collections.Concurrent;

namespace System.Drawing;

internal sealed class HarfBuzzTextEngine : ITextEngine
{
    private static readonly ConcurrentDictionary<TextShapeKey, Lazy<HarfBuzzTextShaper>> s_shapers = new();

    public void DrawString(
        IRenderingBackend backend,
        string text,
        float x,
        float y,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
        DrawShapedString(backend, GetShaper(fontFamily, fontSize, bold, italic), text, x, y, color);
    }

    public void DrawStringAligned(
        IRenderingBackend backend,
        string text,
        RectangleF bounds,
        ContentAlignment alignment,
        Color color,
        string fontFamily,
        float fontSize,
        bool bold,
        bool italic)
    {
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

        DrawString(backend, text, x, y, color, fontFamily, fontSize, bold, italic);
    }

    public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SizeF.Empty;
        }

        return GetShaper(fontFamily, fontSize, bold, italic).Measure(text);
    }

    private static HarfBuzzTextShaper GetShaper(string fontFamily, float fontSize, bool bold, bool italic)
    {
        TextShapeKey key = new(fontFamily, MathF.Max(1f, fontSize), bold, italic);
        return s_shapers.GetOrAdd(key, static value => new Lazy<HarfBuzzTextShaper>(() => CreateShaper(value))).Value;
    }

    private static HarfBuzzTextShaper CreateShaper(TextShapeKey key)
    {
        ResolvedFontFile? font = FontFileResolver.ResolveFontFile(key.FontFamily, key.Bold, key.Italic);
        return font is null
            ? throw new InvalidOperationException($"No font file could be resolved for '{key.FontFamily}'.")
            : new HarfBuzzTextShaper(font.Path, font.FamilyName, key.FontSize, key.Bold, key.Italic);
    }

    private void DrawShapedString(
        IRenderingBackend backend,
        HarfBuzzTextShaper shaper,
        string text,
        float x,
        float y,
        Color color)
    {
        string normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        string[] lines = normalized.Split('\n');
        float lineHeight = MathF.Max(1f, shaper.GetMetrics().LineHeight);
        float currentY = y;
        ShapedFontMetrics metrics = shaper.GetMetrics();
        TrueTypeGlyphOutlineProvider? outlineProvider = null;
        if (backend is ImpellerRenderingBackend)
        {
            outlineProvider = TrueTypeGlyphOutlineProvider.GetOrCreate(shaper.FontPath);
        }

        foreach (string line in lines)
        {
            float cursorX = x;
            float baselineY = currentY + metrics.Ascender;
            foreach (ShapedGlyph glyph in shaper.Shape(line))
            {
                float glyphX = cursorX + glyph.XOffset;
                float glyphY = baselineY - glyph.YOffset;

                if (outlineProvider is not null && backend is ImpellerRenderingBackend impellerBackend)
                {
                    impellerBackend.FillGlyphOutline(
                        outlineProvider.GetGlyph(glyph.GlyphId),
                        glyphX,
                        glyphY,
                        shaper.FontSize / outlineProvider.UnitsPerEm,
                        color);
                }
                else if (glyph.Cluster < line.Length)
                {
                    WinFormsXCompatibilityWarning.Once(
                        "Text.VectorGlyphs.NonImpeller",
                        "Vector font text rendering is currently implemented for the Impeller backend; non-Impeller text draw was ignored.");
                }

                cursorX += glyph.XAdvance;
            }

            currentY += lineHeight;
        }
    }

    private readonly record struct TextShapeKey(string FontFamily, float FontSize, bool Bold, bool Italic);
}

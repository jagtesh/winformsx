using System.IO;
using HarfBuzzSharp;

namespace System.Drawing;

internal readonly record struct ShapedGlyph(
    uint GlyphId,
    uint Cluster,
    float XAdvance,
    float YAdvance,
    float XOffset,
    float YOffset);

internal readonly record struct ShapedFontMetrics(float Ascender, float Descender, float LineGap)
{
    public float LineHeight => Ascender - Descender + LineGap;
}

internal sealed class HarfBuzzTextShaper : IDisposable
{
    private readonly float _fontSize;
    private Blob? _blob;
    private Face? _face;
    private HarfBuzzSharp.Font? _font;
    private bool _disposed;

    public HarfBuzzTextShaper(string fontPath, string familyName, float fontSize, bool bold, bool italic)
    {
        FontPath = fontPath;
        FamilyName = familyName;
        Bold = bold;
        Italic = italic;
        _fontSize = MathF.Max(1f, fontSize);
        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException($"Font file not found: {fontPath}", fontPath);
        }

        _blob = Blob.FromFile(fontPath);
        _face = new Face(_blob, 0);
        _font = new HarfBuzzSharp.Font(_face);
        int scale = checked((int)MathF.Round(_fontSize * 64f));
        _font.SetScale(scale, scale);
        _font.SetFunctionsOpenType();
    }

    public string FamilyName { get; }

    public string FontPath { get; }

    public float FontSize => _fontSize;

    public bool Bold { get; }

    public bool Italic { get; }

    public ShapedGlyph[] Shape(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        ObjectDisposedException.ThrowIf(_font is null, this);

        using var buffer = new HarfBuzzSharp.Buffer();
        try
        {
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            _font.Shape(buffer);

            ReadOnlySpan<GlyphInfo> infos = buffer.GlyphInfos;
            ReadOnlySpan<GlyphPosition> positions = buffer.GlyphPositions;
            ShapedGlyph[] result = new ShapedGlyph[infos.Length];

            for (int i = 0; i < result.Length; i++)
            {
                GlyphInfo info = infos[i];
                GlyphPosition position = positions[i];
                result[i] = new ShapedGlyph(
                    info.Codepoint,
                    info.Cluster,
                    position.XAdvance / 64f,
                    position.YAdvance / 64f,
                    position.XOffset / 64f,
                    position.YOffset / 64f);
            }

            return result;
        }
        finally
        {
        }
    }

    public SizeF Measure(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SizeF.Empty;
        }

        float width = 0f;
        float lineWidth = 0f;
        int lines = 1;
        foreach (string line in text.Replace("\r", string.Empty).Split('\n'))
        {
            lineWidth = 0f;
            foreach (ShapedGlyph glyph in Shape(line))
            {
                lineWidth += glyph.XAdvance;
            }

            width = MathF.Max(width, lineWidth);
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines++;
            }
        }

        ShapedFontMetrics metrics = GetMetrics();
        return new SizeF(width, MathF.Max(_fontSize, metrics.LineHeight) * lines);
    }

    public ShapedFontMetrics GetMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ObjectDisposedException.ThrowIf(_font is null, this);

        if (_font.TryGetHorizontalFontExtents(out FontExtents extents))
        {
            return new ShapedFontMetrics(extents.Ascender / 64f, extents.Descender / 64f, extents.LineGap / 64f);
        }

        return new ShapedFontMetrics(_fontSize * 0.8f, -_fontSize * 0.2f, 0f);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _font?.Dispose();
        _face?.Dispose();
        _blob?.Dispose();
        _font = null;
        _face = null;
        _blob = null;
    }
}

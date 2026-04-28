using System.IO;
using System.Runtime.InteropServices;

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
    private nint _blob;
    private nint _face;
    private nint _font;
    private bool _disposed;

    public HarfBuzzTextShaper(string fontPath, float fontSize)
    {
        _fontSize = MathF.Max(1f, fontSize);
        _blob = HarfBuzzNative.BlobCreateFromFile(fontPath);
        if (_blob == nint.Zero)
        {
            throw new FileNotFoundException($"Font file not found: {fontPath}", fontPath);
        }

        _face = HarfBuzzNative.FaceCreate(_blob, 0);
        _font = HarfBuzzNative.FontCreate(_face);
        int scale = checked((int)MathF.Round(_fontSize * 64f));
        HarfBuzzNative.FontSetScale(_font, scale, scale);
        HarfBuzzNative.OpenTypeFontSetFunctions(_font);
    }

    public ShapedGlyph[] Shape(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        nint buffer = HarfBuzzNative.BufferCreate();
        try
        {
            unsafe
            {
                fixed (char* textPtr = text)
                {
                    HarfBuzzNative.BufferAddUtf16(buffer, (nint)textPtr, text.Length, 0, text.Length);
                }
            }

            HarfBuzzNative.BufferGuessSegmentProperties(buffer);
            HarfBuzzNative.Shape(_font, buffer, nint.Zero, 0);

            nint infos = HarfBuzzNative.BufferGetGlyphInfos(buffer, out uint glyphCount);
            nint positions = HarfBuzzNative.BufferGetGlyphPositions(buffer, out _);
            ShapedGlyph[] result = new ShapedGlyph[glyphCount];

            int infoSize = Marshal.SizeOf<HarfBuzzGlyphInfo>();
            int positionSize = Marshal.SizeOf<HarfBuzzGlyphPosition>();
            for (int i = 0; i < result.Length; i++)
            {
                HarfBuzzGlyphInfo info = Marshal.PtrToStructure<HarfBuzzGlyphInfo>(infos + (i * infoSize));
                HarfBuzzGlyphPosition position = Marshal.PtrToStructure<HarfBuzzGlyphPosition>(positions + (i * positionSize));
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
            HarfBuzzNative.BufferDestroy(buffer);
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

        if (HarfBuzzNative.FontGetHorizontalExtents(_font, out HarfBuzzFontExtents extents))
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
        if (_font != nint.Zero)
        {
            HarfBuzzNative.FontDestroy(_font);
        }

        if (_face != nint.Zero)
        {
            HarfBuzzNative.FaceDestroy(_face);
        }

        if (_blob != nint.Zero)
        {
            HarfBuzzNative.BlobDestroy(_blob);
        }

        _font = nint.Zero;
        _face = nint.Zero;
        _blob = nint.Zero;
    }
}

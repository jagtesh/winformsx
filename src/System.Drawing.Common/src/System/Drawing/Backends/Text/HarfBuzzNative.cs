using System.Runtime.InteropServices;

namespace System.Drawing;

internal static partial class HarfBuzzNative
{
    private const string HarfBuzzLib = "harfbuzz";

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_buffer_create")]
    public static partial nint BufferCreate();

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_buffer_destroy")]
    public static partial void BufferDestroy(nint buffer);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_buffer_add_utf16")]
    public static partial void BufferAddUtf16(nint buffer, nint text, int textLength, uint itemOffset, int itemLength);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_buffer_guess_segment_properties")]
    public static partial void BufferGuessSegmentProperties(nint buffer);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_buffer_get_glyph_infos")]
    public static partial nint BufferGetGlyphInfos(nint buffer, out uint length);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_buffer_get_glyph_positions")]
    public static partial nint BufferGetGlyphPositions(nint buffer, out uint length);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_blob_create_from_file", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint BlobCreateFromFile(string fileName);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_blob_destroy")]
    public static partial void BlobDestroy(nint blob);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_face_create")]
    public static partial nint FaceCreate(nint blob, uint index);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_face_destroy")]
    public static partial void FaceDestroy(nint face);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_font_create")]
    public static partial nint FontCreate(nint face);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_font_destroy")]
    public static partial void FontDestroy(nint font);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_font_set_scale")]
    public static partial void FontSetScale(nint font, int xScale, int yScale);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_ot_font_set_funcs")]
    public static partial void OpenTypeFontSetFunctions(nint font);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_font_get_h_extents")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FontGetHorizontalExtents(nint font, out HarfBuzzFontExtents extents);

    [LibraryImport(HarfBuzzLib, EntryPoint = "hb_shape")]
    public static partial void Shape(nint font, nint buffer, nint features, uint numFeatures);
}

[StructLayout(LayoutKind.Sequential)]
internal struct HarfBuzzGlyphInfo
{
    public uint Codepoint;
    public uint Mask;
    public uint Cluster;
    public uint Var1;
    public uint Var2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HarfBuzzGlyphPosition
{
    public int XAdvance;
    public int YAdvance;
    public int XOffset;
    public int YOffset;
    public uint Var;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HarfBuzzFontExtents
{
    public int Ascender;
    public int Descender;
    public int LineGap;
    public uint Reserved1;
    public uint Reserved2;
    public uint Reserved3;
    public uint Reserved4;
    public uint Reserved5;
    public uint Reserved6;
}

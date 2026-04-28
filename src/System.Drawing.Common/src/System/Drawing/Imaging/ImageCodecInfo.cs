// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Imaging;

// sdkinc\imaging.h
public sealed unsafe class ImageCodecInfo
{
    private static readonly Guid s_bmpCodec = new(0x557cf400, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_jpegCodec = new(0x557cf401, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_gifCodec = new(0x557cf402, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_tiffCodec = new(0x557cf405, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_pngCodec = new(0x557cf406, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);

    internal ImageCodecInfo()
    {
    }

    public Guid Clsid { get; set; }

    public Guid FormatID { get; set; }

    public string? CodecName { get; set; }

    public string? DllName { get; set; }

    public string? FormatDescription { get; set; }

    public string? FilenameExtension { get; set; }

    public string? MimeType { get; set; }

    public ImageCodecFlags Flags { get; set; }

    public int Version { get; set; }

    [CLSCompliant(false)]
    public byte[][]? SignaturePatterns { get; set; }

    [CLSCompliant(false)]
    public byte[][]? SignatureMasks { get; set; }

    // Encoder/Decoder selection APIs

    public static ImageCodecInfo[] GetImageDecoders()
    {
        return GetManagedCodecs();
    }

    public static ImageCodecInfo[] GetImageEncoders()
    {
        return GetManagedCodecs();
    }

    private static ImageCodecInfo[] GetManagedCodecs()
    {
        return
        [
            CreateCodec(s_bmpCodec, PInvokeCore.ImageFormatBMP, "BMP", "*.BMP;*.DIB;*.RLE", "image/bmp"),
            CreateCodec(s_jpegCodec, PInvokeCore.ImageFormatJPEG, "JPEG", "*.JPG;*.JPEG;*.JPE;*.JFIF", "image/jpeg"),
            CreateCodec(s_gifCodec, PInvokeCore.ImageFormatGIF, "GIF", "*.GIF", "image/gif"),
            CreateCodec(s_tiffCodec, PInvokeCore.ImageFormatTIFF, "TIFF", "*.TIF;*.TIFF", "image/tiff"),
            CreateCodec(s_pngCodec, PInvokeCore.ImageFormatPNG, "PNG", "*.PNG", "image/png")
        ];
    }

    private static ImageCodecInfo CreateCodec(Guid codecId, Guid formatId, string name, string extension, string mimeType)
    {
        return new()
        {
            Clsid = codecId,
            FormatID = formatId,
            CodecName = $"Built-in {name} codec",
            DllName = null,
            FormatDescription = name,
            FilenameExtension = extension,
            MimeType = mimeType,
            Flags = ImageCodecFlags.Decoder | ImageCodecFlags.Encoder | ImageCodecFlags.SupportBitmap,
            Version = 1,
            SignaturePatterns = [],
            SignatureMasks = []
        };
    }

}

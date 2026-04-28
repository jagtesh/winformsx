// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32;

namespace System.Drawing;

internal static class ImageCodecInfoHelper
{
    private static readonly Guid s_bmpCodec = new(0x557cf400, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_jpegCodec = new(0x557cf401, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_gifCodec = new(0x557cf402, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_tiffCodec = new(0x557cf405, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
    private static readonly Guid s_pngCodec = new(0x557cf406, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);

    private static readonly (Guid Format, Guid Encoder)[] s_encoders =
    [
        (PInvokeCore.ImageFormatBMP, s_bmpCodec),
        (PInvokeCore.ImageFormatJPEG, s_jpegCodec),
        (PInvokeCore.ImageFormatGIF, s_gifCodec),
        (PInvokeCore.ImageFormatTIFF, s_tiffCodec),
        (PInvokeCore.ImageFormatPNG, s_pngCodec)
    ];

    /// <summary>
    ///  Get the encoder guid for the given image format guid.
    /// </summary>
    internal static Guid GetEncoderClsid(Guid format)
    {
        foreach ((Guid Format, Guid Encoder) in Encoders)
        {
            if (Format == format)
            {
                return Encoder;
            }
        }

        return Guid.Empty;
    }

    private static (Guid Format, Guid Encoder)[] Encoders => s_encoders;
}

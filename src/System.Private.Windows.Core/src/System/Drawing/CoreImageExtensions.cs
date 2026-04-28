// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32;
using Windows.Win32.Graphics.GdiPlus;

namespace System.Drawing;

// We also have an ImageExtensions in Primitives

internal static unsafe class CoreImageExtensions
{
    internal static void Save(this IImage image, Stream stream, Guid encoder, Guid format, EncoderParameters* encoderParameters)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (encoder == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        if (format == PInvokeCore.ImageFormatGIF && image.Data is { } rawData && rawData.Length > 0)
        {
            stream.Write(rawData);
            return;
        }

        throw new NotSupportedException("Native GDI+ image handles are not supported by the managed drawing PAL.");
    }

    internal static void Save(this IImage image, MemoryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new NotSupportedException("Native GDI+ image handles are not supported by the managed drawing PAL.");
    }
}

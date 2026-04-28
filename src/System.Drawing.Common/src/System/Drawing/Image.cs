// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers.Binary;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace System.Drawing;

/// <summary>
///  An abstract base class that provides functionality for 'Bitmap', 'Icon', 'Cursor', and 'Metafile' descended classes.
/// </summary>
[Editor($"System.Drawing.Design.ImageEditor, {AssemblyRef.SystemDrawingDesign}",
        $"System.Drawing.Design.UITypeEditor, {AssemblyRef.SystemDrawing}")]
[ImmutableObject(true)]
[Serializable]
[Runtime.CompilerServices.TypeForwardedFrom(AssemblyRef.SystemDrawing)]
[TypeConverter(typeof(ImageConverter))]
public abstract unsafe class Image : MarshalByRefObject, IImage, IDisposable, ICloneable, ISerializable
{
    // The signature of this delegate is incorrect. The signature of the corresponding
    // native callback function is:
    // extern "C" {
    //     typedef BOOL (CALLBACK * ImageAbort)(VOID *);
    //     typedef ImageAbort DrawImageAbort;
    //     typedef ImageAbort GetThumbnailImageAbort;
    // }
    // However, as this delegate is not used in both GDI 1.0 and 1.1, we choose not
    // to modify it, in order to preserve compatibility.
    public delegate bool GetThumbnailImageAbort();

    nint IPointer<GpImage>.Pointer => (nint)_nativeImage;

    [NonSerialized]
    private GpImage* _nativeImage;
    private int _width;
    private int _height;
    private float _horizontalResolution = 96;
    private float _verticalResolution = 96;
    private Imaging.PixelFormat _pixelFormat = Imaging.PixelFormat.Format32bppArgb;
    private Imaging.ImageFormat _rawFormat = Imaging.ImageFormat.MemoryBmp;
    private bool _disposed;
    private Dictionary<int, Imaging.PropertyItem>? _propertyItems;
    private ColorPalette? _palette;

    private object? _userData;

    // Used to work around lack of animated gif encoder.
    private byte[]? _animatedGifRawData;
    ReadOnlySpan<byte> IRawData.Data => _animatedGifRawData;

    [Localizable(false)]
    [DefaultValue(null)]
    public object? Tag
    {
        get => _userData;
        set => _userData = value;
    }

    private protected Image() { }

#pragma warning disable CA2229 // Implement serialization constructors
    private protected Image(SerializationInfo info, StreamingContext context)
#pragma warning restore CA2229 // Implement serialization constructors
    {
        byte[] dat = (byte[])info.GetValue("Data", typeof(byte[]))!; // Do not rename (binary serialization)

        try
        {
            using Bitmap bitmap = new(new MemoryStream(dat));
            SetManagedImage(
                bitmap.Width,
                bitmap.Height,
                bitmap.PixelFormat,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                bitmap.RawFormat);
        }
        catch (Exception e) when (e is ExternalException
            or ArgumentException
            or OutOfMemoryException
            or InvalidOperationException
            or NotImplementedException
            or FileNotFoundException)
        {
        }
    }

    void ISerializable.GetObjectData(SerializationInfo si, StreamingContext context)
    {
        using MemoryStream stream = new();
        this.Save(stream);
        si.AddValue("Data", stream.ToArray(), typeof(byte[])); // Do not rename (binary serialization)
    }

    /// <summary>
    ///  Creates an <see cref='Image'/> from the specified file.
    /// </summary>
    public static Image FromFile(string filename) => FromFile(filename, false);

    public static Image FromFile(string filename, bool useEmbeddedColorManagement)
    {
        ArgumentNullException.ThrowIfNull(filename, "path");

        if (!File.Exists(filename))
        {
            // Throw a more specific exception for invalid paths that are null or empty,
            // contain invalid characters or are too long.
            filename = Path.GetFullPath(filename);
            throw new FileNotFoundException(filename);
        }

        filename = Path.GetFullPath(filename);
        try
        {
            return new Bitmap(filename, useEmbeddedColorManagement);
        }
        catch (ArgumentException)
        {
            throw Status.OutOfMemory.GetException();
        }
    }

    /// <summary>
    ///  Creates an <see cref='Image'/> from the specified data stream.
    /// </summary>
    public static Image FromStream(Stream stream) => FromStream(stream, useEmbeddedColorManagement: false);

    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement) =>
        FromStream(stream, useEmbeddedColorManagement, true);

    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement, bool validateImageData)
    {
        ArgumentNullException.ThrowIfNull(stream);

        long position = stream.CanSeek ? stream.Position : 0;
        try
        {
            using MemoryStream memory = new();
            stream.CopyTo(memory);
            if (stream.CanSeek)
            {
                stream.Position = position;
            }

            return new Bitmap(new MemoryStream(memory.ToArray()), useEmbeddedColorManagement);
        }
        catch
        {
            if (stream.CanSeek)
            {
                stream.Position = position;
            }

            throw;
        }
    }

    internal Image(GpImage* nativeImage) => SetNativeImage(nativeImage);

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='Image'/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='Image'/>.
    /// </summary>
    ~Image() => Dispose(disposing: false);

    /// <summary>
    ///  Creates an exact copy of this <see cref='Image'/>.
    /// </summary>
    public object Clone()
    {
        ThrowIfDisposed();

        if (_nativeImage is null)
        {
            if (this is Bitmap bitmap)
            {
                return bitmap.CloneManagedBitmap();
            }

            Image clone = (Image)MemberwiseClone();
            clone._propertyItems = ClonePropertyItems();
            clone._palette = ClonePalette(_palette);
            return clone;
        }

        GpImage* cloneImage;
        PInvoke.GdipCloneImage(_nativeImage, &cloneImage).ThrowIfFailed();
        ValidateImage(cloneImage);
        GC.KeepAlive(this);
        return CreateImageObject(cloneImage);
    }

    protected virtual void Dispose(bool disposing)
    {
        _disposed = true;

        if (_nativeImage is null)
        {
            return;
        }

        Status status = !Gdip.Initialized ? Status.Ok : PInvoke.GdipDisposeImage(_nativeImage);
        _nativeImage = null;
        Debug.Assert(status == Status.Ok, $"GDI+ returned an error status: {status}");
    }

    /// <summary>
    ///  Saves this <see cref='Image'/> to the specified file.
    /// </summary>
    public void Save(string filename) => Save(filename, RawFormat);

    /// <summary>
    ///  Saves this <see cref='Image'/> to the specified file in the specified format.
    /// </summary>
    public void Save(string filename, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        Guid encoder = format.Encoder;
        if (encoder == Guid.Empty)
        {
            encoder = ImageCodecInfoHelper.GetEncoderClsid(PInvokeCore.ImageFormatPNG);
        }

        Save(filename, encoder, null);
    }

    /// <summary>
    ///  Saves this <see cref='Image'/> to the specified file in the specified format and with the specified encoder parameters.
    /// </summary>
    public void Save(string filename, ImageCodecInfo encoder, Imaging.EncoderParameters? encoderParams)
        => Save(filename, encoder.Clsid, encoderParams);

    private void Save(string filename, Guid encoder, Imaging.EncoderParameters? encoderParams)
    {
        ArgumentNullException.ThrowIfNull(filename);
        if (encoder == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(encoder));
        }

        ThrowIfDirectoryDoesntExist(filename);

        if (_animatedGifRawData is not null && RawFormat.Encoder == encoder)
        {
            // Special case for animated gifs. We don't have an encoder for them, so we just write the raw data.
            using var fs = File.OpenWrite(filename);
            fs.Write(_animatedGifRawData, 0, _animatedGifRawData.Length);
            return;
        }

        using FileStream stream = File.Create(filename);
        SaveManagedBitmap(stream);
        GC.KeepAlive(encoderParams);
    }

    private void SaveManagedBitmap(Stream stream)
    {
        ThrowIfDisposed();

        int width = Width;
        int height = Height;
        int stride = checked(((width * 3) + 3) & ~3);
        int pixelDataSize = checked(stride * height);
        int fileSize = checked(14 + 40 + pixelDataSize);

        Span<byte> fileHeader = stackalloc byte[14];
        fileHeader[0] = (byte)'B';
        fileHeader[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(fileHeader[2..6], fileSize);
        BinaryPrimitives.WriteInt32LittleEndian(fileHeader[10..14], 54);
        stream.Write(fileHeader);

        Span<byte> dibHeader = stackalloc byte[40];
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[0..4], 40);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[4..8], width);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[8..12], height);
        BinaryPrimitives.WriteInt16LittleEndian(dibHeader[12..14], 1);
        BinaryPrimitives.WriteInt16LittleEndian(dibHeader[14..16], 24);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[20..24], pixelDataSize);
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[24..28], (int)Math.Round(HorizontalResolution * 39.3701f));
        BinaryPrimitives.WriteInt32LittleEndian(dibHeader[28..32], (int)Math.Round(VerticalResolution * 39.3701f));
        stream.Write(dibHeader);

        Span<byte> row = stride <= 4096 ? stackalloc byte[stride] : new byte[stride];
        ReadOnlySpan<int> pixels = this is Bitmap bitmap ? bitmap.ManagedPixels : [];
        for (int y = height - 1; y >= 0; y--)
        {
            row.Clear();
            for (int x = 0; x < width; x++)
            {
                int argb = pixels.IsEmpty ? Color.Black.ToArgb() : pixels[(y * width) + x];
                int offset = x * 3;
                row[offset] = (byte)argb;
                row[offset + 1] = (byte)(argb >> 8);
                row[offset + 2] = (byte)(argb >> 16);
            }

            stream.Write(row);
        }
    }

    /// <summary>
    ///  Saves this <see cref='Image'/> to the specified stream in the specified format.
    /// </summary>
    public void Save(Stream stream, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        this.Save(stream, format.Encoder, format.Guid, encoderParameters: null);
    }

    /// <summary>
    ///  Saves this <see cref='Image'/> to the specified stream in the specified format.
    /// </summary>
    public void Save(Stream stream, ImageCodecInfo encoder, Imaging.EncoderParameters? encoderParams)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoder);

        SaveManagedBitmap(stream);
        GC.KeepAlive(encoderParams);
    }

    /// <summary>
    ///  Adds an <see cref='EncoderParameters'/> to this <see cref='Image'/>.
    /// </summary>
    public void SaveAdd(Imaging.EncoderParameters? encoderParams)
    {
        _animatedGifRawData = null;
        GC.KeepAlive(this);
        GC.KeepAlive(encoderParams);
        throw new NotSupportedException("Multi-frame image encoding is not implemented in the managed drawing PAL yet.");
    }

    /// <summary>
    ///  Adds an <see cref='EncoderParameters'/> to the specified <see cref='Image'/>.
    /// </summary>
    public void SaveAdd(Image image, Imaging.EncoderParameters? encoderParams)
    {
        ArgumentNullException.ThrowIfNull(image);

        _animatedGifRawData = null;
        GC.KeepAlive(this);
        GC.KeepAlive(image);
        GC.KeepAlive(encoderParams);
        throw new NotSupportedException("Multi-frame image encoding is not implemented in the managed drawing PAL yet.");
    }

    private static void ThrowIfDirectoryDoesntExist(string filename)
    {
        string? directoryPart = Path.GetDirectoryName(filename);
        if (!string.IsNullOrEmpty(directoryPart) && !Directory.Exists(directoryPart))
        {
            throw new DirectoryNotFoundException(SR.Format(SR.TargetDirectoryDoesNotExist, directoryPart, filename));
        }
    }

    /// <summary>
    ///  Gets the width and height of this <see cref='Image'/>.
    /// </summary>
    public SizeF PhysicalDimension
    {
        get
        {
            if (_nativeImage is null)
            {
                return new SizeF(_width, _height);
            }

            float width;
            float height;

            PInvoke.GdipGetImageDimension(_nativeImage, &width, &height).ThrowIfFailed();
            GC.KeepAlive(this);
            return new SizeF(width, height);
        }
    }

    /// <summary>
    ///  Gets the width and height of this <see cref='Image'/>.
    /// </summary>
    public Size Size => new(Width, Height);

    /// <summary>
    ///  Gets the width of this <see cref='Image'/>.
    /// </summary>
    [DefaultValue(false)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Width
    {
        get
        {
            if (_nativeImage is null)
            {
                ThrowIfDisposed();
                return _width;
            }

            uint width;
            PInvoke.GdipGetImageWidth(_nativeImage, &width).ThrowIfFailed();
            GC.KeepAlive(this);
            return (int)width;
        }
    }

    /// <summary>
    ///  Gets the height of this <see cref='Image'/>.
    /// </summary>
    [DefaultValue(false)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Height
    {
        get
        {
            if (_nativeImage is null)
            {
                ThrowIfDisposed();
                return _height;
            }

            uint height;
            PInvoke.GdipGetImageHeight(_nativeImage, &height).ThrowIfFailed();
            GC.KeepAlive(this);
            return (int)height;
        }
    }

    /// <summary>
    ///  Gets the horizontal resolution, in pixels-per-inch, of this <see cref='Image'/>.
    /// </summary>
    public float HorizontalResolution
    {
        get
        {
            if (_nativeImage is null)
            {
                return _horizontalResolution;
            }

            float horzRes;
            PInvoke.GdipGetImageHorizontalResolution(_nativeImage, &horzRes).ThrowIfFailed();
            GC.KeepAlive(this);
            return horzRes;
        }
    }

    /// <summary>
    ///  Gets the vertical resolution, in pixels-per-inch, of this <see cref='Image'/>.
    /// </summary>
    public float VerticalResolution
    {
        get
        {
            if (_nativeImage is null)
            {
                return _verticalResolution;
            }

            float vertRes;
            PInvoke.GdipGetImageVerticalResolution(_nativeImage, &vertRes).ThrowIfFailed();
            GC.KeepAlive(this);
            return vertRes;
        }
    }

    /// <summary>
    ///  Gets attribute flags for this <see cref='Image'/>.
    /// </summary>
    [Browsable(false)]
    public int Flags
    {
        get
        {
            if (_nativeImage is null)
            {
                return 0;
            }

            uint flags;
            PInvoke.GdipGetImageFlags(_nativeImage, &flags).ThrowIfFailed();
            GC.KeepAlive(this);
            return (int)flags;
        }
    }

    /// <summary>
    ///  Gets the format of this <see cref='Image'/>.
    /// </summary>
    public ImageFormat RawFormat
    {
        get
        {
            if (_nativeImage is null)
            {
                return _rawFormat;
            }

            Guid guid = default;
            PInvoke.GdipGetImageRawFormat(_nativeImage, &guid).ThrowIfFailed();
            GC.KeepAlive(this);
            return new ImageFormat(guid);
        }
    }

    /// <summary>
    ///  Gets the pixel format for this <see cref='Image'/>.
    /// </summary>
    public PixelFormat PixelFormat => _nativeImage is null ? _pixelFormat : (PixelFormat)this.GetPixelFormat();

    internal void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ArgumentException(SR.GdiplusInvalidParameter);
        }
    }

    /// <summary>
    ///  Gets an array of the property IDs stored in this <see cref='Image'/>.
    /// </summary>
    [Browsable(false)]
    public int[] PropertyIdList
    {
        get
        {
            uint count;
            if (_nativeImage is null)
            {
                if (_propertyItems is null || _propertyItems.Count == 0)
                {
                    return [];
                }

                int[] ids = new int[_propertyItems.Count];
                _propertyItems.Keys.CopyTo(ids, 0);
                return ids;
            }

            PInvoke.GdipGetPropertyCount(_nativeImage, &count).ThrowIfFailed();
            if (count == 0)
            {
                return [];
            }

            int[] propid = new int[count];
            fixed (int* pPropid = propid)
            {
                PInvoke.GdipGetPropertyIdList(_nativeImage, count, (uint*)pPropid).ThrowIfFailed();
            }

            GC.KeepAlive(this);
            return propid;
        }
    }

    /// <summary>
    ///  Gets an array of <see cref='Imaging.PropertyItem'/> objects that describe this <see cref='Image'/>.
    /// </summary>
    [Browsable(false)]
    public Imaging.PropertyItem[] PropertyItems
    {
        get
        {
            uint size, count;
            if (_nativeImage is null)
            {
                if (_propertyItems is null || _propertyItems.Count == 0)
                {
                    return [];
                }

                Imaging.PropertyItem[] items = new Imaging.PropertyItem[_propertyItems.Count];
                int index = 0;
                foreach (Imaging.PropertyItem item in _propertyItems.Values)
                {
                    items[index++] = ClonePropertyItem(item);
                }

                return items;
            }

            PInvoke.GdipGetPropertySize(_nativeImage, &size, &count).ThrowIfFailed();

            if (size == 0 || count == 0)
            {
                return [];
            }

            Imaging.PropertyItem[] result = new Imaging.PropertyItem[(int)count];
            using BufferScope<byte> buffer = new((int)size);
            fixed (byte* b = buffer)
            {
                GdiPlus.PropertyItem* properties = (GdiPlus.PropertyItem*)b;
                PInvoke.GdipGetAllPropertyItems(_nativeImage, size, count, properties);

                for (int i = 0; i < count; i++)
                {
                    result[i] = Imaging.PropertyItem.FromNative(properties + i);
                }
            }

            GC.KeepAlive(this);
            return result;
        }
    }

    /// <summary>
    ///  Gets a bounding rectangle in the specified units for this <see cref='Image'/>.
    /// </summary>
    public RectangleF GetBounds(ref GraphicsUnit pageUnit)
    {
        // The Unit is hard coded to GraphicsUnit.Pixel in GDI+.
        RectangleF bounds = _nativeImage is null ? new RectangleF(0, 0, _width, _height) : this.GetImageBounds();
        pageUnit = GraphicsUnit.Pixel;
        return bounds;
    }

    /// <summary>
    ///  Gets or sets the color palette used for this <see cref='Image'/>.
    /// </summary>
    [Browsable(false)]
    public ColorPalette Palette
    {
        get
        {
            ThrowIfDisposed();
            if (_nativeImage is null)
            {
                return _palette ??= CreateDefaultPalette(_pixelFormat);
            }

            // "size" is total byte size:
            // sizeof(ColorPalette) + (pal->Count-1)*sizeof(ARGB)

            int size;
            PInvoke.GdipGetImagePaletteSize(_nativeImage, &size).ThrowIfFailed();

            using BufferScope<uint> buffer = new(size / sizeof(uint));
            fixed (uint* b = buffer)
            {
                PInvoke.GdipGetImagePalette(_nativeImage, (GdiPlus.ColorPalette*)b, size).ThrowIfFailed();
                GC.KeepAlive(this);
                return ColorPalette.ConvertFromBuffer(buffer);
            }
        }
        set
        {
            ThrowIfDisposed();
            if (_nativeImage is null)
            {
                _ = value.Flags;
                _palette = ClonePalette(value);
                return;
            }

            using BufferScope<uint> buffer = value.ConvertToBuffer();
            fixed (uint* b = buffer)
            {
                PInvoke.GdipSetImagePalette(_nativeImage, (GdiPlus.ColorPalette*)b).ThrowIfFailed();
                GC.KeepAlive(this);
            }
        }
    }

    // Thumbnail support

    /// <summary>
    ///  Returns the thumbnail for this <see cref='Image'/>.
    /// </summary>
    public Image GetThumbnailImage(int thumbWidth, int thumbHeight, GetThumbnailImageAbort? callback, IntPtr callbackData)
    {
        Bitmap thumbnail = new(this, thumbWidth, thumbHeight);
        GC.KeepAlive(this);
        GC.KeepAlive(callback);
        return thumbnail;
    }

    internal static void ValidateImage(GpImage* image)
    {
        try
        {
            PInvoke.GdipImageForceValidation(image).ThrowIfFailed();
        }
        catch
        {
            PInvoke.GdipDisposeImage(image);
            throw;
        }
    }

    /// <summary>
    ///  Returns the number of frames of the given dimension.
    /// </summary>
    public int GetFrameCount(FrameDimension dimension)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(dimension);

        if (_nativeImage is null)
        {
            return 1;
        }

        Guid dimensionID = dimension.Guid;
        uint count;
        PInvoke.GdipImageGetFrameCount(_nativeImage, &dimensionID, &count).ThrowIfFailed();
        GC.KeepAlive(this);
        return (int)count;
    }

    /// <summary>
    ///  Gets the specified property item from this <see cref='Image'/>.
    /// </summary>
    public Imaging.PropertyItem? GetPropertyItem(int propid)
    {
        if (_nativeImage is null)
        {
            if (_propertyItems is not null && _propertyItems.TryGetValue(propid, out Imaging.PropertyItem? item))
            {
                return ClonePropertyItem(item);
            }

            throw new ArgumentException(SR.GdiplusPropertyNotFoundError);
        }

        uint size;
        PInvoke.GdipGetPropertyItemSize(_nativeImage, (uint)propid, &size).ThrowIfFailed();

        if (size == 0)
        {
            return null;
        }

        using BufferScope<byte> buffer = new((int)size);
        fixed (byte* b = buffer)
        {
            GdiPlus.PropertyItem* property = (GdiPlus.PropertyItem*)b;
            PInvoke.GdipGetPropertyItem(_nativeImage, (uint)propid, size, property).ThrowIfFailed();
            GC.KeepAlive(this);
            return Imaging.PropertyItem.FromNative(property);
        }
    }

    /// <summary>
    ///  Selects the frame specified by the given dimension and index.
    /// </summary>
    public int SelectActiveFrame(FrameDimension dimension, int frameIndex)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(dimension);

        if (_nativeImage is null)
        {
            return 0;
        }

        Guid dimensionID = dimension.Guid;
        PInvoke.GdipImageSelectActiveFrame(_nativeImage, &dimensionID, (uint)frameIndex).ThrowIfFailed();
        GC.KeepAlive(this);
        return 0;
    }

    /// <summary>
    ///  Sets the specified property item to the specified value.
    /// </summary>
    public unsafe void SetPropertyItem(Imaging.PropertyItem propitem)
    {
        ArgumentNullException.ThrowIfNull(propitem);

        if (_nativeImage is null)
        {
            _propertyItems ??= [];
            _propertyItems[propitem.Id] = ClonePropertyItem(propitem);
            return;
        }

        fixed (byte* propItemValue = propitem.Value)
        {
            GdiPlus.PropertyItem native = new()
            {
                id = (uint)propitem.Id,
                length = (uint)propitem.Len,
                type = (ushort)propitem.Type,
                value = propItemValue
            };

            PInvoke.GdipSetPropertyItem(_nativeImage, &native).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    public void RotateFlip(RotateFlipType rotateFlipType)
    {
        ThrowIfDisposed();

        if (_nativeImage is null)
        {
            return;
        }

        PInvoke.GdipImageRotateFlip(_nativeImage, (GdiPlus.RotateFlipType)rotateFlipType).ThrowIfFailed();
        GC.KeepAlive(this);
    }

    /// <summary>
    ///  Removes the specified property item from this <see cref='Image'/>.
    /// </summary>
    public void RemovePropertyItem(int propid)
    {
        if (_nativeImage is null)
        {
            if (_propertyItems is null || _propertyItems.Count == 0)
            {
                throw Gdip.StatusException(Status.GenericError);
            }

            if (!_propertyItems.Remove(propid))
            {
                throw new ArgumentException(SR.GdiplusPropertyNotFoundError);
            }

            return;
        }

        PInvoke.GdipRemovePropertyItem(_nativeImage, (uint)propid).ThrowIfFailed();
        GC.KeepAlive(this);
    }

    /// <summary>
    ///  Returns information about the codecs used for this <see cref='Image'/>.
    /// </summary>
    public unsafe Imaging.EncoderParameters? GetEncoderParameterList(Guid encoder)
    {
        return CreateEncoderParameterList(encoder);
    }

    /// <summary>
    ///  Creates a <see cref='Bitmap'/> from a Windows handle.
    /// </summary>
    public static Bitmap FromHbitmap(IntPtr hbitmap) => FromHbitmap(hbitmap, IntPtr.Zero);

    /// <summary>
    ///  Creates a <see cref='Bitmap'/> from the specified Windows handle with the specified color palette.
    /// </summary>
    public static Bitmap FromHbitmap(IntPtr hbitmap, IntPtr hpalette)
    {
        throw Status.GenericError.GetException();
    }

    /// <summary>
    ///  Returns a value indicating whether the pixel format is extended.
    /// </summary>
    public static bool IsExtendedPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Extended) != 0;

    /// <summary>
    ///  Returns a value indicating whether the pixel format is canonical.
    /// </summary>
    public static bool IsCanonicalPixelFormat(PixelFormat pixfmt)
    {
        // Canonical formats:
        //
        //  PixelFormat32bppARGB
        //  PixelFormat32bppPARGB
        //  PixelFormat64bppARGB
        //  PixelFormat64bppPARGB

        return (pixfmt & PixelFormat.Canonical) != 0;
    }

    internal void SetNativeImage(GpImage* handle)
    {
        if (handle is null)
            throw new ArgumentException(SR.NativeHandle0, nameof(handle));

        _disposed = false;
        _nativeImage = handle;
    }

    internal void SetManagedImage(
        int width,
        int height,
        PixelFormat pixelFormat,
        float horizontalResolution = 96,
        float verticalResolution = 96,
        ImageFormat? rawFormat = null)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException(SR.GdiplusInvalidSize);
        }

        _disposed = false;
        _nativeImage = null;
        _width = width;
        _height = height;
        _pixelFormat = pixelFormat;
        _horizontalResolution = horizontalResolution;
        _verticalResolution = verticalResolution;
        _rawFormat = rawFormat ?? ImageFormat.MemoryBmp;
    }

    internal void SetManagedPropertyItem(Imaging.PropertyItem item)
    {
        _propertyItems ??= [];
        _propertyItems[item.Id] = ClonePropertyItem(item);
    }

    // Multi-frame support

    /// <summary>
    ///  Gets an array of GUIDs that represent the dimensions of frames within this <see cref='Image'/>.
    /// </summary>
    [Browsable(false)]
    public unsafe Guid[] FrameDimensionsList
    {
        get
        {
            uint count;
            if (_nativeImage is null)
            {
                return [FrameDimension.Page.Guid];
            }

            PInvoke.GdipImageGetFrameDimensionsCount(_nativeImage, &count).ThrowIfFailed();

            Debug.Assert(count >= 0, "FrameDimensionsList returns bad count");
            if (count <= 0)
            {
                return [];
            }

            Guid[] guids = new Guid[count];
            fixed (Guid* g = guids)
            {
                PInvoke.GdipImageGetFrameDimensionsList(_nativeImage, g, count).ThrowIfFailed();
            }

            GC.KeepAlive(this);
            return guids;
        }
    }

    /// <summary>
    ///  Returns the size of the specified pixel format.
    /// </summary>
    public static int GetPixelFormatSize(PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;

    /// <summary>
    ///  Returns a value indicating whether the pixel format contains alpha information.
    /// </summary>
    public static bool IsAlphaPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Alpha) != 0;

    internal static Image CreateImageObject(GpImage* nativeImage)
    {
        GdiPlus.ImageType imageType = default;
        PInvoke.GdipGetImageType(nativeImage, &imageType);
        return imageType switch
        {
            GdiPlus.ImageType.ImageTypeBitmap => new Bitmap((GpBitmap*)nativeImage),
            GdiPlus.ImageType.ImageTypeMetafile => new Metafile((nint)nativeImage),
            _ => throw new ArgumentException(SR.InvalidImage),
        };
    }

    private Dictionary<int, Imaging.PropertyItem>? ClonePropertyItems()
    {
        if (_propertyItems is null)
        {
            return null;
        }

        Dictionary<int, Imaging.PropertyItem> clone = [];
        foreach (KeyValuePair<int, Imaging.PropertyItem> item in _propertyItems)
        {
            clone.Add(item.Key, ClonePropertyItem(item.Value));
        }

        return clone;
    }

    private static Imaging.PropertyItem ClonePropertyItem(Imaging.PropertyItem item) =>
        new()
        {
            Id = item.Id,
            Len = item.Len,
            Type = item.Type,
            Value = item.Value is null ? null : (byte[])item.Value.Clone()
        };

    private static ColorPalette? ClonePalette(ColorPalette? palette) =>
        palette is null ? null : ColorPalette.Create(palette.Flags, (Color[])palette.Entries.Clone());

    private static ColorPalette CreateDefaultPalette(PixelFormat format)
    {
        if (format == PixelFormat.Format1bppIndexed)
        {
            return ColorPalette.Create(0, [Color.Black, Color.White]);
        }

        if (format == PixelFormat.Format4bppIndexed)
        {
            return ColorPalette.Create(0, CreateDefault8BitPalette(16));
        }

        if (format == PixelFormat.Format8bppIndexed)
        {
            return ColorPalette.Create(0, CreateDefault8BitPalette(256));
        }

        return ColorPalette.Create(0, []);
    }

    private static Imaging.EncoderParameters? CreateEncoderParameterList(Guid encoder)
    {
        Guid jpegCodec = new(0x557cf401, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);
        Guid tiffCodec = new(0x557cf405, 0x1a04, 0x11d3, [0x9a, 0x73, 0x00, 0x00, 0xf8, 0x1e, 0xf3, 0x2e]);

        if (encoder == jpegCodec)
        {
            return CreateEncoderParameters(
                Imaging.Encoder.Transformation,
                Imaging.Encoder.Quality,
                Imaging.Encoder.LuminanceTable,
                Imaging.Encoder.ChrominanceTable,
                Imaging.Encoder.ImageItems);
        }

        if (encoder == tiffCodec)
        {
            return CreateEncoderParameters(
                Imaging.Encoder.Compression,
                Imaging.Encoder.ColorDepth,
                Imaging.Encoder.SaveFlag,
                Imaging.Encoder.SaveAsCmyk);
        }

        return null;
    }

    private static Imaging.EncoderParameters CreateEncoderParameters(params Imaging.Encoder[] encoders)
    {
        Imaging.EncoderParameters parameters = new(encoders.Length);
        for (int i = 0; i < encoders.Length; i++)
        {
            parameters.Param[i] = new Imaging.EncoderParameter(encoders[i], 0L);
        }

        return parameters;
    }

    private static Color[] CreateDefault8BitPalette(int count)
    {
        Color[] colors = new Color[count];
        int[] systemColors =
        [
            unchecked((int)0xFF000000), unchecked((int)0xFF800000), unchecked((int)0xFF008000), unchecked((int)0xFF808000),
            unchecked((int)0xFF000080), unchecked((int)0xFF800080), unchecked((int)0xFF008080), unchecked((int)0xFF808080),
            unchecked((int)0xFFC0C0C0), unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), unchecked((int)0xFFFFFF00),
            unchecked((int)0xFF0000FF), unchecked((int)0xFFFF00FF), unchecked((int)0xFF00FFFF), unchecked((int)0xFFFFFFFF)
        ];

        for (int i = 0; i < Math.Min(count, systemColors.Length); i++)
        {
            colors[i] = Color.FromArgb(systemColors[i]);
        }

        if (count <= 16)
        {
            return colors;
        }

        int index = 40;
        for (int red = 0; red < 256; red += 51)
        {
            for (int green = 0; green < 256; green += 51)
            {
                for (int blue = 0; blue < 256; blue += 51)
                {
                    colors[index++] = Color.FromArgb(255, red, green, blue);
                }
            }
        }

        for (int value = 0; index < colors.Length; value += 17)
        {
            colors[index++] = Color.FromArgb(255, value, value, value);
        }

        return colors;
    }

    /// <summary>
    ///  If the image is an animated GIF, loads the raw data for the image into the _rawData field so we
    ///  can work around the lack of an animated GIF encoder.
    /// </summary>
    internal static unsafe void GetAnimatedGifRawData(Image image, string? filename, Stream? dataStream)
    {
        if (!image.RawFormat.Equals(ImageFormat.Gif))
        {
            return;
        }

        bool animatedGif = false;

        uint dimensions;
        PInvoke.GdipImageGetFrameDimensionsCount(image._nativeImage, &dimensions).ThrowIfFailed();
        if (dimensions <= 0)
        {
            return;
        }

        using BufferScope<Guid> guids = new(stackalloc Guid[16], (int)dimensions);

        fixed (Guid* g = guids)
        {
            PInvoke.GdipImageGetFrameDimensionsList(image._nativeImage, g, dimensions).ThrowIfFailed();
        }

        Guid timeGuid = FrameDimension.Time.Guid;
        for (int i = 0; i < dimensions; i++)
        {
            if (timeGuid == guids[i])
            {
                animatedGif = image.GetFrameCount(FrameDimension.Time) > 1;
                break;
            }
        }

        if (!animatedGif)
        {
            return;
        }

        try
        {
            Stream? created = null;
            long lastPos = 0;
            if (dataStream is not null)
            {
                lastPos = dataStream.Position;
                dataStream.Position = 0;
            }

            try
            {
                if (dataStream is null)
                {
                    created = dataStream = File.OpenRead(filename ?? throw new InvalidOperationException());
                }

                image._animatedGifRawData = new byte[(int)dataStream.Length];
                dataStream.Read(image._animatedGifRawData, 0, (int)dataStream.Length);
            }
            finally
            {
                if (created is not null)
                {
                    created.Close();
                }
                else
                {
                    dataStream!.Position = lastPos;
                }
            }
        }
        catch (Exception e) when (e
            // possible exceptions for reading the filename
            is UnauthorizedAccessException
            or DirectoryNotFoundException
            or IOException
            // possible exceptions for setting/getting the position inside dataStream
            or NotSupportedException
            or ObjectDisposedException
            // possible exception when reading stuff into dataStream
            or ArgumentException)
        {
        }
    }
}

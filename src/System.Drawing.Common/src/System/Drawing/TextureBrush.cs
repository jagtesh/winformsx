// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing.Imaging;

namespace System.Drawing;

public sealed unsafe class TextureBrush : Brush
{
    private readonly Image _image;
    private RectangleF _dstRect;
    private ImageAttributes? _imageAttributes;
    private Matrix _transform = new();
    private Drawing2D.WrapMode _wrapMode;

    public TextureBrush(Image bitmap) : this(bitmap, Drawing2D.WrapMode.Tile)
    {
    }

    public TextureBrush(Image image, Drawing2D.WrapMode wrapMode)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateWrapMode(wrapMode);
        _image = image;
        _wrapMode = wrapMode;
        _dstRect = new RectangleF(0, 0, image.Width, image.Height);
    }

    public TextureBrush(Image image, Drawing2D.WrapMode wrapMode, RectangleF dstRect)
        : this(image, wrapMode)
    {
        _dstRect = dstRect;
    }

    public TextureBrush(Image image, Drawing2D.WrapMode wrapMode, Rectangle dstRect)
        : this(image, wrapMode, (RectangleF)dstRect)
    {
    }

    public TextureBrush(Image image, RectangleF dstRect) : this(image, dstRect, null) { }

    public TextureBrush(Image image, RectangleF dstRect, ImageAttributes? imageAttr)
        : this(image, Drawing2D.WrapMode.Tile, dstRect)
    {
        _imageAttributes = imageAttr;
    }

    public TextureBrush(Image image, Rectangle dstRect) : this(image, dstRect, null) { }

    public TextureBrush(Image image, Rectangle dstRect, ImageAttributes? imageAttr)
        : this(image, (RectangleF)dstRect, imageAttr)
    {
    }

    internal TextureBrush(GpTexture* nativeBrush)
    {
        Debug.Assert(nativeBrush is not null, "Initializing native brush with null.");
        _image = new Bitmap(1, 1);
        _dstRect = new RectangleF(0, 0, 1, 1);
        _wrapMode = Drawing2D.WrapMode.Tile;
    }

    public override object Clone()
    {
        TextureBrush clone = new(_image, _wrapMode, _dstRect)
        {
            _imageAttributes = _imageAttributes,
            _transform = (Matrix)_transform.Clone()
        };

        return clone;
    }

    public Matrix Transform
    {
        get => (Matrix)_transform.Clone();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _transform = (Matrix)value.Clone();
        }
    }

    public Drawing2D.WrapMode WrapMode
    {
        get => _wrapMode;
        set
        {
            ValidateWrapMode(value);
            _wrapMode = value;
        }
    }

    public Image Image => _image;

    public void ResetTransform() => _transform.Reset();

    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        _transform.Multiply(matrix, order);
    }

    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform.Translate(dx, dy, order);

    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    public void ScaleTransform(float sx, float sy, MatrixOrder order) => _transform.Scale(sx, sy, order);

    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    public void RotateTransform(float angle, MatrixOrder order) => _transform.Rotate(angle, order);

    private static void ValidateWrapMode(Drawing2D.WrapMode value)
    {
        if (value is < Drawing2D.WrapMode.Tile or > Drawing2D.WrapMode.Clamp)
        {
            throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Drawing2D.WrapMode));
        }
    }
}

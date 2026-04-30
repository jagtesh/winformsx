// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls;

namespace System.Windows.Forms.Primitives.Tests.Interop.ComCtl32;

public class ImageListCompatibilityTests
{
    [Fact]
    public void ImageList_StatefulOperations_ReturnExpectedValues()
    {
        HIMAGELIST himl = PInvoke.ImageList_Create(16, 20, IMAGELIST_CREATION_FLAGS.ILC_COLOR32, 0, 4);
        var handle = new HandleRef<HIMAGELIST>(this, himl);
        try
        {
            Assert.False(himl.IsNull);
            Assert.True(PInvoke.ImageList.GetIconSize(handle, out int width, out int height));
            Assert.Equal(16, width);
            Assert.Equal(20, height);
            Assert.Equal(0, PInvoke.ImageList.GetImageCount(handle));

            Assert.Equal(0, PInvoke.ImageList.Add(handle, default, default));
            Assert.Equal(1, PInvoke.ImageList.ReplaceIcon(handle, -1, new HandleRef<HICON>(this, default)));
            Assert.Equal(2, PInvoke.ImageList.GetImageCount(handle));

            Assert.True(PInvoke.ImageList.GetImageInfo(handle, 0, out IMAGEINFO info));
            Assert.False(info.hbmImage.IsNull);
            Assert.Equal(16, info.rcImage.Width);
            Assert.Equal(20, info.rcImage.Height);

            Assert.True(PInvoke.GetObject(info.hbmImage, out BITMAP bitmap));
            Assert.Equal(16, bitmap.bmWidth);
            Assert.Equal(20, bitmap.bmHeight);
            Assert.Equal(32, bitmap.bmBitsPixel);

            COLORREF red = new(0x000000FF);
            Assert.Equal(unchecked((uint)PInvoke.CLR_NONE), PInvoke.ImageList.SetBkColor(handle, red).Value);
            Assert.Equal(red, PInvoke.ImageList.SetBkColor(handle, (COLORREF)PInvoke.CLR_NONE));

            Assert.True(PInvoke.ImageList.SetIconSize(handle, 24, 28));
            Assert.True(PInvoke.ImageList.GetIconSize(handle, out width, out height));
            Assert.Equal(24, width);
            Assert.Equal(28, height);
            Assert.Equal(0, PInvoke.ImageList.GetImageCount(handle));
            Assert.False(PInvoke.ImageList.GetImageInfo(handle, 0, out _));

            Assert.Equal(0, PInvoke.ImageList.Add(handle, default, default));
            Assert.True(PInvoke.ImageList.GetImageInfo(handle, 0, out info));
            Assert.True(PInvoke.GetObject(info.hbmImage, out bitmap));
            Assert.Equal(24, bitmap.bmWidth);
            Assert.Equal(28, bitmap.bmHeight);
        }
        finally
        {
            PInvoke.ImageList.Destroy(handle);
        }
    }

    [Fact]
    public void ImageList_Write_ReturnsSuccessForKnownHandle()
    {
        HIMAGELIST himl = PInvoke.ImageList_Create(16, 16, IMAGELIST_CREATION_FLAGS.ILC_COLOR32, 0, 4);
        var handle = new HandleRef<HIMAGELIST>(this, himl);
        try
        {
            using MemoryStream stream = new();
            Assert.True(PInvoke.ImageList.Write(handle, stream));
            Assert.Equal(HRESULT.S_OK, PInvoke.ImageList.WriteEx(handle, IMAGE_LIST_WRITE_STREAM_FLAGS.ILP_DOWNLEVEL, stream));
        }
        finally
        {
            PInvoke.ImageList.Destroy(handle);
        }
    }
}

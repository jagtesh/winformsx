// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Com;

namespace Windows.Win32;

internal partial class PInvoke
{
    public static HIMAGELIST ImageList_Create(
        int cx,
        int cy,
        IMAGELIST_CREATION_FLAGS flags,
        int cInitial,
        int cGrow) => ImageList.ImageList_Create(cx, cy, flags, cInitial, cGrow);

    public static HIMAGELIST ImageList_Duplicate(HIMAGELIST himl) => ImageList.ImageList_Duplicate(himl);

    public static unsafe HIMAGELIST ImageList_Read(IStream* pstm) => ImageList.ImageList_Read(pstm);

    public static unsafe class ImageList
    {
        private static int s_nextImageListHandle = 0x2000;
        private static readonly Dictionary<nint, ImageListState> s_imageLists = [];

        private sealed class ImageListState(int width, int height, int count)
        {
            public int Width { get; } = width;
            public int Height { get; } = height;
            public int Count { get; set; } = count;
        }

        private static nint Key(HIMAGELIST himl) => (nint)himl.Value;

        private static HIMAGELIST CreateImageListHandle(int width, int height, int count)
        {
            nint handle = (nint)Interlocked.Increment(ref s_nextImageListHandle);
            s_imageLists[handle] = new ImageListState(width, height, count);
            return (HIMAGELIST)handle;
        }

        private static ImageListState? GetState(HIMAGELIST himl) =>
            s_imageLists.TryGetValue(Key(himl), out ImageListState? state) ? state : null;

        private static int ImageList_Add(HIMAGELIST himl, HBITMAP hbmImage, HBITMAP hbmMask)
        {
            ImageListState? state = GetState(himl);
            if (state is null)
            {
                return -1;
            }

            return state.Count++;
        }

        internal static HIMAGELIST ImageList_Create(int cx, int cy, IMAGELIST_CREATION_FLAGS flags, int cInitial, int cGrow) =>
            CreateImageListHandle(cx, cy, 0);

        private static BOOL ImageList_Destroy(HIMAGELIST himl)
        {
            s_imageLists.Remove(Key(himl));
            return true;
        }

        private static BOOL ImageList_Draw(HIMAGELIST himl, int i, HDC hdcDst, int x, int y, IMAGE_LIST_DRAW_STYLE fStyle) => true;

        private static BOOL ImageList_DrawEx(
            HIMAGELIST himl,
            int i,
            HDC hdcDst,
            int x,
            int y,
            int dx,
            int dy,
            COLORREF rgbBk,
            COLORREF rgbFg,
            IMAGE_LIST_DRAW_STYLE fStyle) => true;

        internal static HIMAGELIST ImageList_Duplicate(HIMAGELIST himl)
        {
            ImageListState? state = GetState(himl);
            return state is null
                ? HIMAGELIST.Null
                : CreateImageListHandle(state.Width, state.Height, state.Count);
        }

        private static BOOL ImageList_GetIconSize(HIMAGELIST himl, int* cx, int* cy)
        {
            ImageListState? state = GetState(himl);
            if (state is null)
            {
                return false;
            }

            *cx = state.Width;
            *cy = state.Height;
            return true;
        }

        private static int ImageList_GetImageCount(HIMAGELIST himl) => GetState(himl)?.Count ?? 0;

        private static BOOL ImageList_GetImageInfo(HIMAGELIST himl, int i, out IMAGEINFO pImageInfo)
        {
            pImageInfo = default;
            return false;
        }

        internal static HIMAGELIST ImageList_Read(IStream* pstm) => CreateImageListHandle(16, 16, 0);

        private static BOOL ImageList_Remove(HIMAGELIST himl, int i)
        {
            ImageListState? state = GetState(himl);
            if (state is null)
            {
                return false;
            }

            if (i == -1)
            {
                state.Count = 0;
            }
            else if (state.Count > 0)
            {
                state.Count--;
            }

            return true;
        }

        private static BOOL ImageList_Replace(HIMAGELIST himl, int i, HBITMAP hbmImage, HBITMAP hbmMask)
        {
            ImageListState? state = GetState(himl);
            return state is not null && i >= 0 && i < state.Count;
        }

        private static int ImageList_ReplaceIcon(HIMAGELIST himl, int i, HICON hicon)
        {
            ImageListState? state = GetState(himl);
            if (state is null)
            {
                return -1;
            }

            if (i == -1)
            {
                return state.Count++;
            }

            return i >= 0 && i < state.Count ? i : -1;
        }

        private static COLORREF ImageList_SetBkColor(HIMAGELIST himl, COLORREF clrBk) => clrBk;

        private static BOOL ImageList_Write(HIMAGELIST himl, IStream* pstm) => true;

        private static HRESULT ImageList_WriteEx(HIMAGELIST himl, IMAGE_LIST_WRITE_STREAM_FLAGS dwFlags, IStream* pstm) =>
            HRESULT.S_OK;

        /// <inheritdoc cref="ImageList_Add(HIMAGELIST, HBITMAP, HBITMAP)"/>
        public static int Add<T>(T himl, HBITMAP hbmImage, HBITMAP hbmMask) where T : IHandle<HIMAGELIST>
        {
            int result = ImageList_Add(himl.Handle, hbmImage, hbmMask);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_Destroy(HIMAGELIST)"/>
        public static bool Destroy<T>(T himl) where T : IHandle<HIMAGELIST>
        {
            bool result = ImageList_Destroy(himl.Handle);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_Draw(HIMAGELIST, int, HDC, int, int, IMAGE_LIST_DRAW_STYLE)"/>
        public static bool Draw<T>(T himl, int i, HDC hdcDst, int x, int y, IMAGE_LIST_DRAW_STYLE fStyle)
            where T : IHandle<HIMAGELIST>
        {
            BOOL result = ImageList_Draw(himl.Handle, i, hdcDst, x, y, fStyle);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_DrawEx(HIMAGELIST, int, HDC, int, int, int, int, COLORREF, COLORREF, IMAGE_LIST_DRAW_STYLE)"/>
        public static bool DrawEx<THIML, THDC>(
            THIML himl,
            int i,
            THDC hdcDst,
            int x,
            int y,
            int dx,
            int dy,
            COLORREF rgbBk,
            COLORREF rgbFg,
            IMAGE_LIST_DRAW_STYLE fStyle) where THIML : IHandle<HIMAGELIST> where THDC : IHandle<HDC>
        {
            bool result = ImageList_DrawEx(himl.Handle, i, hdcDst.Handle, x, y, dx, dy, rgbBk, rgbFg, fStyle);
            GC.KeepAlive(himl.Wrapper);
            GC.KeepAlive(hdcDst.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_GetIconSize(HIMAGELIST, int*, int*)"/>
        public static bool GetIconSize<T>(T himl, out int x, out int y) where T : IHandle<HIMAGELIST>
        {
            int width = 0;
            int height = 0;
            bool result = ImageList_GetIconSize(himl.Handle, &width, &height);
            x = width;
            y = height;
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        public static bool SetIconSize<T>(T himl, int x, int y) where T : IHandle<HIMAGELIST>
        {
            ImageListState? state = GetState(himl.Handle);
            bool result = state is not null;
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_GetImageCount(HIMAGELIST)"/>
        public static int GetImageCount<T>(T himl) where T : IHandle<HIMAGELIST>
        {
            int result = ImageList_GetImageCount(himl.Handle);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        public static void EnsureImageCount<T>(T himl, int count) where T : IHandle<HIMAGELIST>
        {
            ImageListState? state = GetState(himl.Handle);
            if (state is not null && state.Count < count)
            {
                state.Count = count;
            }

            GC.KeepAlive(himl.Wrapper);
        }

        /// <inheritdoc cref="ImageList_GetImageInfo(HIMAGELIST, int, IMAGEINFO*)"/>
        public static bool GetImageInfo<T>(T himl, int i, out IMAGEINFO pImageInfo) where T : IHandle<HIMAGELIST>
        {
            pImageInfo = default;
            bool result = false;
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_Remove(HIMAGELIST, int)"/>
        public static bool Remove<T>(T himl, int i) where T : IHandle<HIMAGELIST>
        {
            bool result = ImageList_Remove(himl.Handle, i);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_Replace(HIMAGELIST, int, HBITMAP, HBITMAP)"/>
        public static bool Replace<T>(T himl, int i, HBITMAP hbmImage, HBITMAP hbmMask) where T : IHandle<HIMAGELIST>
        {
            bool result = ImageList_Replace(himl.Handle, i, hbmImage, hbmMask);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_ReplaceIcon(HIMAGELIST, int, HICON)"/>
        public static int ReplaceIcon<THIML, THICON>(
            THIML himl,
            int i,
            THICON hicon) where THIML : IHandle<HIMAGELIST> where THICON : IHandle<HICON>
        {
            int result = ImageList_ReplaceIcon(himl.Handle, i, hicon.Handle);
            GC.KeepAlive(himl.Wrapper);
            GC.KeepAlive(hicon.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_SetBkColor(HIMAGELIST, COLORREF)"/>
        public static COLORREF SetBkColor<T>(T himl, COLORREF clrBk) where T : IHandle<HIMAGELIST>
        {
            COLORREF result = ImageList_SetBkColor(himl.Handle, clrBk);
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_Write(HIMAGELIST, IStream*)"/>
        public static BOOL Write<T>(T himl, Stream pstm) where T : IHandle<HIMAGELIST>
        {
            BOOL result = false;
            GC.KeepAlive(himl.Wrapper);
            return result;
        }

        /// <inheritdoc cref="ImageList_WriteEx(HIMAGELIST, IMAGE_LIST_WRITE_STREAM_FLAGS, IStream*)"/>
        public static HRESULT WriteEx<T>(
            T himl,
            IMAGE_LIST_WRITE_STREAM_FLAGS dwFlags,
            Stream pstm) where T : IHandle<HIMAGELIST>
        {
            HRESULT result = HRESULT.E_NOTIMPL;
            GC.KeepAlive(himl.Wrapper);
            return result;
        }
    }
}

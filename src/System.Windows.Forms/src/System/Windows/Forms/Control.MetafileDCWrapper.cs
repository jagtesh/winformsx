// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms;

public partial class Control
{
    /// <summary>
    ///  MetafileDCWrapper is used to wrap a metafile DC so that subsequent
    ///  paint operations are rendered to a temporary bitmap.  When the
    ///  wrapper is disposed, it copies the bitmap back to the metafile DC.
    ///
    ///  Example:
    ///
    ///  using(MetafileDCWrapper dcWrapper = new MetafileDCWrapper(hDC, size) {
    ///  // ...use dcWrapper.HDC to do painting
    ///  }
    /// </summary>
    private class MetafileDCWrapper : IDisposable
    {
        private static int s_nextSyntheticHdc = 1;

        internal MetafileDCWrapper(HDC hOriginalDC, Size size)
        {
            if (size.Width < 0 || size.Height < 0)
            {
                throw new ArgumentException(SR.ControlMetaFileDCWrapperSizeInvalid, nameof(size));
            }

            HDC = CreateSyntheticHdc();
        }

        ~MetafileDCWrapper()
        {
            ((IDisposable)this).Dispose();
        }

        void IDisposable.Dispose()
        {
            HDC = default;
            GC.SuppressFinalize(this);
        }

        internal HDC HDC { get; private set; }

        private static HDC CreateSyntheticHdc() => (HDC)(nint)Interlocked.Increment(ref s_nextSyntheticHdc);
    }
}

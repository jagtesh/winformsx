// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms;

internal sealed partial class FontCache
{
    internal struct Data : IDisposable
    {
        public WeakReference<Font> Font { get; }
        public HFONT HFONT { get; private set; }
        public FONT_QUALITY Quality { get; }

        private int? _tmHeight;

        public Data(Font font, FONT_QUALITY quality)
        {
            Font = new WeakReference<Font>(font);
            Quality = quality;
            HFONT = FromFont(font, quality);
            _tmHeight = null;
        }

        public int Height
        {
            get
            {
                _tmHeight ??= Font.TryGetTarget(out Font? font)
                    ? (int)Math.Ceiling(font.GetHeight(ScaleHelper.InitialSystemDpi))
                    : ScaleHelper.InitialSystemDpi / 6;

                return _tmHeight.Value;
            }
        }

        public void Dispose()
        {
            HFONT = default;
        }

        private static HFONT FromFont(Font font, FONT_QUALITY quality = FONT_QUALITY.DEFAULT_QUALITY) => (HFONT)font.ToHfont();
    }
}

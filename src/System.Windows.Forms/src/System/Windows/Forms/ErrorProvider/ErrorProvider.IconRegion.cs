// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms;

public partial class ErrorProvider
{
    /// <summary>
    ///  This represents the visible bounds of the icon.
    /// </summary>
    internal class IconRegion : IHandle<HICON>
    {
        private Region? _region;
        private readonly Icon _icon;

        public IconRegion(Icon icon, int currentDpi)
        {
            _icon = ScaleHelper.ScaleSmallIconToDpi(icon, currentDpi);
        }

        /// <summary>
        ///  Returns the icon.
        /// </summary>
        public Icon Icon => _icon;

        /// <summary>
        ///  Returns the handle of the icon.
        /// </summary>
        public HICON Handle => (HICON)_icon.Handle;

        /// <summary>
        ///  Returns the handle of the region.
        /// </summary>
        public Region Region
        {
            get
            {
                if (_region is not null)
                {
                    return _region;
                }

                _region = new Region(new Rectangle(Point.Empty, _icon.Size));

                return _region;
            }
        }

        /// <summary>
        ///  Return the size of the icon.
        /// </summary>
        public Size Size => _icon.Size;

        /// <summary>
        ///  Release any resources held by this Object.
        /// </summary>
        public void Dispose()
        {
            _region?.Dispose();
            _region = null;
        }
    }
}

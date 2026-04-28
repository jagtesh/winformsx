// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Drawing.Drawing2D;

namespace System.Windows.Forms.Design;

public partial class ComponentEditorForm
{
    // This should be moved into a shared location
    //  Its a duplication of what exists in the StyleBuilder.
    internal sealed class PageSelector : TreeView
    {
        private const int PADDING_VERT = 3;
        private const int PADDING_HORZ = 4;

        private const int SIZE_ICON_X = 16;
        private const int SIZE_ICON_Y = 16;

        private const int STATE_NORMAL = 0;
        private const int STATE_SELECTED = 1;
        private const int STATE_HOT = 2;

        private bool _hasDitherBrush;

        public PageSelector()
        {
            HotTracking = true;
            HideSelection = false;
            BackColor = SystemColors.Control;
            Indent = 0;
            LabelEdit = false;
            Scrollable = false;
            ShowLines = false;
            ShowPlusMinus = false;
            ShowRootLines = false;
            BorderStyle = BorderStyle.None;
            Indent = 0;
            FullRowSelect = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;

                cp.ExStyle |= (int)WINDOW_EX_STYLE.WS_EX_STATICEDGE;
                return cp;
            }
        }

        private void CreateDitherBrush()
        {
            Debug.Assert(!_hasDitherBrush, "Brush should not be recreated.");
            _hasDitherBrush = true;
        }

        private unsafe void DrawTreeItem(
            string itemText,
            int imageIndex,
            HDC dc,
            RECT rcIn,
            int state,
            COLORREF backColor,
            COLORREF textColor)
        {
            RECT rc = rcIn;
            ImageList? imageList = ImageList;

            GC.KeepAlive(Parent);
            using Graphics graphics = dc.CreateGraphics();
            Rectangle bounds = Rectangle.FromLTRB(rc.left, rc.top, rc.right, rc.bottom);
            Color managedBackColor = ColorTranslator.FromWin32((int)backColor.Value);
            Color managedTextColor = ColorTranslator.FromWin32((int)textColor.Value);

            // Fill the background
            if (((state & STATE_SELECTED) != 0) && _hasDitherBrush)
            {
                FillRectDither(graphics, bounds);
            }
            else
            {
                using SolidBrush backgroundBrush = new(managedBackColor);
                graphics.FillRectangle(backgroundBrush, bounds);
            }

            // Draw the caption
            Rectangle textBounds = new(
                rc.left + SIZE_ICON_X + 2 * PADDING_HORZ,
                rc.top,
                Math.Max(0, rc.Width - SIZE_ICON_X - 2 * PADDING_HORZ),
                rc.Height);
            using SolidBrush textBrush = new(managedTextColor);
            using StringFormat stringFormat = new(StringFormat.GenericTypographic)
            {
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };
            stringFormat.FormatFlags |= StringFormatFlags.NoWrap;
            graphics.DrawString(itemText, Parent?.Font ?? Font, textBrush, textBounds, stringFormat);

            if (imageList is not null)
            {
                imageList.Draw(graphics, PADDING_HORZ, rc.top + (((rc.bottom - rc.top) - SIZE_ICON_Y) >> 1), imageIndex);
            }

            // Draw the hot-tracking border if needed
            if ((state & STATE_HOT) != 0)
            {
                ControlPaint.DrawBorder3D(graphics, bounds, Border3DStyle.RaisedInner);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            int itemHeight = (int)PInvoke.SendMessage(this, PInvoke.TVM_GETITEMHEIGHT);
            itemHeight += 2 * PADDING_VERT;
            PInvoke.SendMessage(this, PInvoke.TVM_SETITEMHEIGHT, (WPARAM)itemHeight);

            if (!_hasDitherBrush)
            {
                CreateDitherBrush();
            }
        }

        private unsafe void OnCustomDraw(ref Message m)
        {
            NMTVCUSTOMDRAW* nmtvcd = (NMTVCUSTOMDRAW*)(nint)m.LParamInternal;

            switch (nmtvcd->nmcd.dwDrawStage)
            {
                case NMCUSTOMDRAW_DRAW_STAGE.CDDS_PREPAINT:
                    m.ResultInternal = (LRESULT)(nint)(PInvoke.CDRF_NOTIFYITEMDRAW | PInvoke.CDRF_NOTIFYPOSTPAINT);
                    break;
                case NMCUSTOMDRAW_DRAW_STAGE.CDDS_ITEMPREPAINT:
                    {
                        TreeNode? itemNode = TreeNode.FromHandle(this, (nint)nmtvcd->nmcd.dwItemSpec);
                        if (itemNode is not null)
                        {
                            int state = STATE_NORMAL;
                            NMCUSTOMDRAW_DRAW_STATE_FLAGS itemState = nmtvcd->nmcd.uItemState;
                            if (((itemState & NMCUSTOMDRAW_DRAW_STATE_FLAGS.CDIS_HOT) != 0) ||
                                ((itemState & NMCUSTOMDRAW_DRAW_STATE_FLAGS.CDIS_FOCUS) != 0))
                            {
                                state |= STATE_HOT;
                            }

                            if ((itemState & NMCUSTOMDRAW_DRAW_STATE_FLAGS.CDIS_SELECTED) != 0)
                            {
                                state |= STATE_SELECTED;
                            }

                            DrawTreeItem(
                                itemNode.Text,
                                itemNode.ImageIndex,
                                nmtvcd->nmcd.hdc,
                                nmtvcd->nmcd.rc,
                                state,
                                (COLORREF)(uint)ColorTranslator.ToWin32(SystemColors.Control),
                                (COLORREF)(uint)ColorTranslator.ToWin32(SystemColors.ControlText));
                        }

                        m.ResultInternal = (LRESULT)(nint)PInvoke.CDRF_SKIPDEFAULT;
                    }

                    break;
                case NMCUSTOMDRAW_DRAW_STAGE.CDDS_POSTPAINT:
                    m.ResultInternal = (LRESULT)(nint)PInvoke.CDRF_SKIPDEFAULT;
                    break;
                default:
                    m.ResultInternal = (LRESULT)(nint)PInvoke.CDRF_DODEFAULT;
                    break;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);

            if (!RecreatingHandle && _hasDitherBrush)
            {
                _hasDitherBrush = false;
            }
        }

        private static void FillRectDither(Graphics graphics, Rectangle bounds)
        {
            using HatchBrush brush = new(HatchStyle.Percent50, SystemColors.ControlLightLight, SystemColors.Control);
            graphics.FillRectangle(brush, bounds);
        }

        protected override unsafe void WndProc(ref Message m)
        {
            if (m.MsgInternal == MessageId.WM_REFLECT_NOTIFY)
            {
                NMHDR* nmhdr = (NMHDR*)(nint)m.LParamInternal;
                if (nmhdr->code == PInvoke.NM_CUSTOMDRAW)
                {
                    OnCustomDraw(ref m);
                    return;
                }
            }

            base.WndProc(ref m);
        }
    }
}

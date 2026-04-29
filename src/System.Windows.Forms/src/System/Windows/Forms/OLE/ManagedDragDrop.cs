// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms;

internal static class ManagedDragDrop
{
    private const int MK_LBUTTON = 0x0001;
    private const int MK_RBUTTON = 0x0002;
    private const int MK_SHIFT = 0x0004;
    private const int MK_CONTROL = 0x0008;
    private const int MK_MBUTTON = 0x0010;
    private const int MK_ALT = 0x0020;
    private const int MouseButtonMask = MK_LBUTTON | MK_RBUTTON | MK_MBUTTON;

    internal static DragDropEffects DoDragDrop(
        ISupportOleDropSource source,
        Control? sourceControl,
        IDataObject dataObject,
        DragDropEffects allowedEffects,
        Bitmap? dragImage,
        Point cursorOffset,
        bool useDefaultDragImage)
    {
        IDropTarget? currentTarget = null;
        DragEventArgs? lastDragEvent = null;
        DragDropEffects currentEffect = DragDropEffects.None;
        bool dropped = false;
        bool raisedDragEvent = false;

        Stopwatch timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();

            int keyState = GetCurrentKeyState();
            DragAction action = (keyState & MouseButtonMask) == 0 ? DragAction.Drop : DragAction.Continue;
            QueryContinueDragEventArgs queryContinue = new(keyState, escapePressed: false, action);
            source.OnQueryContinueDrag(queryContinue);

            if (queryContinue.Action == DragAction.Drop && !raisedDragEvent)
            {
                queryContinue.Action = DragAction.Continue;
            }

            if (!raisedDragEvent)
            {
                IDropTarget? primedTarget = FindDropTarget(Control.MousePosition, sourceControl);
                if (primedTarget is not null)
                {
                    DragEventArgs primedEvent = CreateDragEvent(dataObject, keyState, allowedEffects, currentEffect);
                    primedTarget.OnDragEnter(primedEvent);
                    primedTarget.OnDragOver(primedEvent);
                    currentTarget = primedTarget;
                    currentEffect = primedEvent.Effect;
                    lastDragEvent = primedEvent.Clone();
                    raisedDragEvent = true;
                }
            }

            if (queryContinue.Action == DragAction.Cancel)
            {
                currentTarget?.OnDragLeave(EventArgs.Empty);
                return DragDropEffects.None;
            }

            if (queryContinue.Action == DragAction.Drop)
            {
                if (currentTarget is not null)
                {
                    DragEventArgs dragEvent = lastDragEvent ?? CreateDragEvent(dataObject, keyState, allowedEffects, currentEffect);
                    currentTarget.OnDragDrop(dragEvent);
                    currentEffect = dragEvent.Effect;
                    dropped = true;
                }

                break;
            }

            IDropTarget? nextTarget = FindDropTarget(Control.MousePosition, sourceControl);
            if (!ReferenceEquals(nextTarget, currentTarget))
            {
                currentTarget?.OnDragLeave(EventArgs.Empty);
                currentTarget = nextTarget;
                lastDragEvent = null;
            }

            if (currentTarget is not null)
            {
                DragEventArgs dragEvent = CreateDragEvent(dataObject, keyState, allowedEffects, currentEffect);
                if (lastDragEvent is null)
                {
                    currentTarget.OnDragEnter(dragEvent);
                }
                else
                {
                    if (lastDragEvent.DropImageType > DropImageType.Invalid)
                    {
                        dragEvent.DropImageType = lastDragEvent.DropImageType;
                        dragEvent.Message = lastDragEvent.Message;
                        dragEvent.MessageReplacementToken = lastDragEvent.MessageReplacementToken;
                    }

                    currentTarget.OnDragOver(dragEvent);
                }

                currentEffect = dragEvent.Effect;
                lastDragEvent = dragEvent.Clone();
                raisedDragEvent = true;
            }
            else
            {
                currentEffect = DragDropEffects.None;
                lastDragEvent = null;
            }

            source.OnGiveFeedback(new GiveFeedbackEventArgs(currentEffect, useDefaultCursors: true, dragImage, cursorOffset, useDefaultDragImage));
            Thread.Sleep(10);
        }

        if (!dropped)
        {
            currentTarget?.OnDragLeave(EventArgs.Empty);
            return DragDropEffects.None;
        }

        return currentEffect;
    }

    private static IDropTarget? FindDropTarget(Point screenPoint, Control? sourceControl)
    {
        Control? pointedControl = Control.FromChildHandle(PInvoke.WindowFromPoint(screenPoint));
        IDropTarget? pointedTarget = FindAllowDropTarget(pointedControl);
        if (pointedTarget is not null)
        {
            if (sourceControl is null || !ReferenceEquals(pointedTarget, sourceControl))
            {
                return pointedTarget;
            }

            IDropTarget? alternate = FindFormFallbackTarget(sourceControl, excludeSource: true);
            return alternate ?? pointedTarget;
        }

        if (sourceControl is not null)
        {
            IDropTarget? alternate = FindFormFallbackTarget(sourceControl, excludeSource: true);
            if (alternate is not null)
            {
                return alternate;
            }

            if (sourceControl.AllowDrop && sourceControl is IDropTarget sourceDropTarget)
            {
                return sourceDropTarget;
            }
        }

        return null;
    }

    private static IDropTarget? FindAllowDropTarget(Control? control)
    {
        while (control is not null)
        {
            if (control.AllowDrop && control is IDropTarget dropTarget)
            {
                return dropTarget;
            }

            control = control.ParentInternal;
        }

        return null;
    }

    private static IDropTarget? FindFormFallbackTarget(Control sourceControl, bool excludeSource)
    {
        Control? root = sourceControl.FindForm() ?? sourceControl.TopLevelControl as Control ?? sourceControl;
        return FindFirstAllowDropTarget(root, sourceControl, excludeSource);
    }

    private static IDropTarget? FindFirstAllowDropTarget(Control control, Control sourceControl, bool excludeSource)
    {
        if (control.AllowDrop
            && control is IDropTarget dropTarget
            && (!excludeSource || !ReferenceEquals(control, sourceControl)))
        {
            return dropTarget;
        }

        foreach (Control child in control.Controls)
        {
            IDropTarget? result = FindFirstAllowDropTarget(child, sourceControl, excludeSource);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static DragEventArgs CreateDragEvent(
        IDataObject dataObject,
        int keyState,
        DragDropEffects allowedEffects,
        DragDropEffects effect)
    {
        Point mousePosition = Control.MousePosition;
        return new DragEventArgs(dataObject, keyState, mousePosition.X, mousePosition.Y, allowedEffects, effect);
    }

    private static int GetCurrentKeyState()
    {
        int keyState = 0;
        MouseButtons mouseButtons = Control.MouseButtons;
        if ((mouseButtons & MouseButtons.Left) == MouseButtons.Left)
        {
            keyState |= MK_LBUTTON;
        }

        if ((mouseButtons & MouseButtons.Right) == MouseButtons.Right)
        {
            keyState |= MK_RBUTTON;
        }

        if ((mouseButtons & MouseButtons.Middle) == MouseButtons.Middle)
        {
            keyState |= MK_MBUTTON;
        }

        Keys modifierKeys = Control.ModifierKeys;
        if ((modifierKeys & Keys.Shift) == Keys.Shift)
        {
            keyState |= MK_SHIFT;
        }

        if ((modifierKeys & Keys.Control) == Keys.Control)
        {
            keyState |= MK_CONTROL;
        }

        if ((modifierKeys & Keys.Alt) == Keys.Alt)
        {
            keyState |= MK_ALT;
        }

        return keyState;
    }
}

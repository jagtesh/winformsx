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
    private static int s_dragOperationDepth;

    internal static bool IsInProgress => Volatile.Read(ref s_dragOperationDepth) > 0;

    internal static DragDropEffects DoDragDrop(
        ISupportOleDropSource source,
        Control? sourceControl,
        IDataObject dataObject,
        DragDropEffects allowedEffects,
        Bitmap? dragImage,
        Point cursorOffset,
        bool useDefaultDragImage)
    {
        // Managed drag/drop pumps the message loop and can re-enter the source MouseMove
        // path. Ignore nested calls so a single gesture yields a single drag operation.
        if (IsInProgress)
        {
            return DragDropEffects.None;
        }

        Interlocked.Increment(ref s_dragOperationDepth);
        try
        {
        bool verbose = string.Equals(
            Environment.GetEnvironmentVariable("WINFORMSX_DRAG_VERBOSE"),
            "1",
            StringComparison.Ordinal);
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

            if (verbose)
            {
                Console.WriteLine($"[ManagedDragDrop] keyState=0x{keyState:X} action={queryContinue.Action} target={(currentTarget as Control)?.Name ?? currentTarget?.GetType().Name ?? "<null>"}");
            }

            if (!raisedDragEvent)
            {
                IDropTarget? primedTarget = FindDropTarget(Control.MousePosition, sourceControl);
                if (primedTarget is not null)
                {
                    DragEventArgs primedEvent = CreateDragEvent(dataObject, keyState, allowedEffects, currentEffect);
                    primedTarget.OnDragEnter(primedEvent);
                    primedTarget.OnDragOver(primedEvent);
                    if (verbose)
                    {
                        Console.WriteLine($"[ManagedDragDrop] primed target={(primedTarget as Control)?.Name ?? primedTarget.GetType().Name} effect={primedEvent.Effect} hasButton={primedEvent.Data?.GetDataPresent(typeof(Button))}");
                    }

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
                    if (verbose)
                    {
                        Console.WriteLine($"[ManagedDragDrop] drop target={(currentTarget as Control)?.Name ?? currentTarget.GetType().Name} effect={dragEvent.Effect} hasButton={dragEvent.Data?.GetDataPresent(typeof(Button))}");
                    }

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
                    currentTarget.OnDragOver(dragEvent);
                    if (verbose)
                    {
                        Console.WriteLine($"[ManagedDragDrop] enter+over target={(currentTarget as Control)?.Name ?? currentTarget.GetType().Name} effect={dragEvent.Effect} hasButton={dragEvent.Data?.GetDataPresent(typeof(Button))}");
                    }
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
                    if (verbose)
                    {
                        Console.WriteLine($"[ManagedDragDrop] over target={(currentTarget as Control)?.Name ?? currentTarget.GetType().Name} effect={dragEvent.Effect} hasButton={dragEvent.Data?.GetDataPresent(typeof(Button))}");
                    }
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
        finally
        {
            Interlocked.Decrement(ref s_dragOperationDepth);
        }
    }

    private static IDropTarget? FindDropTarget(Point screenPoint, Control? sourceControl)
    {
        Control? root = sourceControl?.FindForm() ?? sourceControl?.TopLevelControl as Control ?? sourceControl;
        IDropTarget? pointedTarget = null;
        if (root is not null)
        {
            Control? pointedControl = FindDeepestControlAtScreenPoint(root, screenPoint);
            pointedTarget = FindAllowDropTarget(pointedControl);
        }

        if (pointedTarget is not null)
        {
            if (sourceControl is null || !ReferenceEquals(pointedTarget, sourceControl))
            {
                return pointedTarget;
            }

            IDropTarget? alternate = FindNearestAllowDropTarget(root, screenPoint, sourceControl, excludeSource: true);
            return alternate ?? pointedTarget;
        }

        if (sourceControl is not null)
        {
            IDropTarget? alternate = FindNearestAllowDropTarget(root, screenPoint, sourceControl, excludeSource: true);
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

    private static IDropTarget? FindNearestAllowDropTarget(Control? root, Point screenPoint, Control sourceControl, bool excludeSource)
    {
        if (root is null)
        {
            return null;
        }

        IDropTarget? nearest = null;
        long bestDistance = long.MaxValue;
        foreach (Control control in EnumerateControls(root))
        {
            if (!control.AllowDrop || control is not IDropTarget dropTarget)
            {
                continue;
            }

            if (excludeSource && ReferenceEquals(control, sourceControl))
            {
                continue;
            }

            Rectangle bounds = GetScreenBounds(control);
            Point center = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            long dx = screenPoint.X - center.X;
            long dy = screenPoint.Y - center.Y;
            long distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = dropTarget;
            }
        }

        return nearest;
    }

    private static Control? FindDeepestControlAtScreenPoint(Control root, Point screenPoint)
    {
        if (!root.Visible || !GetScreenBounds(root).Contains(screenPoint))
        {
            return null;
        }

        for (int i = root.Controls.Count - 1; i >= 0; i--)
        {
            Control child = root.Controls[i];
            Control? hit = FindDeepestControlAtScreenPoint(child, screenPoint);
            if (hit is not null)
            {
                return hit;
            }
        }

        return root;
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        Stack<Control> stack = new();
        stack.Push(root);

        while (stack.Count > 0)
        {
            Control current = stack.Pop();
            yield return current;

            for (int i = current.Controls.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Controls[i]);
            }
        }
    }

    private static Rectangle GetScreenBounds(Control control)
    {
        Point topLeft = control.PointToScreen(Point.Empty);
        return new Rectangle(topLeft, control.Size);
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

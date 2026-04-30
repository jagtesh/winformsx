// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Drawing;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller message interop — internal message queue replacing the Win32 message pump.
/// Messages are dispatched through a managed queue rather than the OS.
/// </summary>
internal sealed class ImpellerMessageInterop : IMessageInterop
{
    private const uint WM_QUIT = 0x0012;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_ERASEBKGND = 0x0014;
    private const uint WAIT_OBJECT_0 = 0x00000000;
    private const uint WAIT_TIMEOUT = 0x00000102;
    private const uint EM_GETSEL = 0x00B0;
    private const uint EM_SETSEL = 0x00B1;
    private const uint EM_SETMODIFY = 0x00B9;
    private const uint EM_REPLACESEL = 0x00C2;
    private const uint EM_LIMITTEXT = 0x00C5;
    private const uint EM_SETREADONLY = 0x00CF;

    private readonly ConcurrentQueue<ImpellerMessage> _messageQueue = new();
    private readonly Dictionary<string, uint> _registeredMessages = [];
    private readonly Dictionary<HWND, TextSelectionState> _textSelections = [];
    private readonly Dictionary<HWND, ListViewTileViewState> _listViewTileViews = [];
    private readonly HashSet<HWND> _listViewGroupHeaderFocus = [];
    private uint _nextRegisteredMessage = 0xC000; // WM_APP range

    public LRESULT SendMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.MessageDispatch,
            $"SendMessage hwnd=0x{(nint)hWnd:X} msg=0x{msg:X}");

        if (TryHandleTextBoxMessage(hWnd, msg, wParam, lParam, out LRESULT textResult))
        {
            return textResult;
        }

        if (TryHandleListViewMessage(hWnd, msg, wParam, lParam, out LRESULT listViewResult))
        {
            return listViewResult;
        }

        if (TryHandleToolTipMessage(msg, out LRESULT toolTipResult))
        {
            return toolTipResult;
        }

        if (msg == PInvoke.LVM_ENABLEGROUPVIEW
            || msg == PInvoke.LVM_ISGROUPVIEWENABLED
            || msg == PInvoke.LVM_HASGROUP
            || msg == PInvoke.LVM_SETITEMW)
        {
            return (LRESULT)1;
        }

        // Synchronous dispatch — look up the NativeWindow for this HWND and invoke
        // its WndProc directly (wxWidgets-style synthetic message dispatch).
        // WM_PAINT flows through the standard WinForms WmPaint pipeline:
        //   BeginPaintScope → ImpellerGdiInterop.BeginPaint (acquires Impeller surface)
        //   → PaintEventArgs → Graphics.FromHdcInternal (creates backend-backed Graphics)
        //   → OnPaint → EndPaint (presents frame)
        if (NativeWindow.DispatchMessageDirect(hWnd, msg, wParam, lParam, out LRESULT result))
        {
            return result;
        }

        return (LRESULT)0;
    }

    public LRESULT SendMessage(HWND hWnd, MessageId msg, WPARAM wParam, LPARAM lParam)
        => SendMessage(hWnd, (uint)msg, wParam, lParam);

    public bool PostMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        _messageQueue.Enqueue(new ImpellerMessage(hWnd, msg, wParam, lParam));
        return true;
    }

    public bool PeekMessage(out MSG lpMsg, HWND hWnd, uint filterMin, uint filterMax, PEEK_MESSAGE_REMOVE_TYPE flags)
    {
        if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
        {
            windowInterop.PumpEvents();
        }

        if (_messageQueue.TryPeek(out var im))
        {
            lpMsg = im.ToMSG();
            if (flags.HasFlag(PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
            {
                _messageQueue.TryDequeue(out _);
            }

            return true;
        }

        lpMsg = default;
        return false;
    }

    public bool GetMessage(out MSG lpMsg, HWND hWnd, uint filterMin, uint filterMax)
    {
        // Blocking — spin until a message arrives
        // TODO: Use a proper wait handle for efficiency
        ImpellerMessage im;
        SpinWait spin = default;
        while (!_messageQueue.TryDequeue(out im))
        {
            if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
            {
                windowInterop.PumpEvents();
            }

            spin.SpinOnce();
        }

        lpMsg = im.ToMSG();
        return lpMsg.message != WM_QUIT;
    }

    public bool TranslateMessage(in MSG lpMsg) => true; // No-op in Impeller
    public LRESULT DispatchMessage(in MSG lpMsg) => SendMessage(lpMsg.hwnd, lpMsg.message, lpMsg.wParam, lpMsg.lParam);

    public unsafe uint MsgWaitForMultipleObjectsEx(
        uint nCount,
        HANDLE* pHandles,
        uint dwMilliseconds,
        QUEUE_STATUS_FLAGS dwWakeMask,
        MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS dwFlags)
    {
        _ = pHandles;
        _ = dwWakeMask;
        _ = dwFlags;

        if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
        {
            windowInterop.PumpEvents();
        }

        if (nCount == 0 && _messageQueue.TryPeek(out _))
        {
            return WAIT_OBJECT_0;
        }

        if (dwMilliseconds != 0)
        {
            int sleepMilliseconds = dwMilliseconds == uint.MaxValue
                ? 15
                : Math.Min(15, checked((int)Math.Min(dwMilliseconds, int.MaxValue)));

            Thread.Sleep(sleepMilliseconds);
        }

        return WAIT_TIMEOUT;
    }

    public unsafe bool SendMessageTimeout(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam,
        SEND_MESSAGE_TIMEOUT_FLAGS flags, uint timeout, nuint* result)
    {
        var r = SendMessage(hWnd, msg, wParam, lParam);
        if (result != null)
            *result = (nuint)(nint)r;
        return true;
    }

    public bool SendMessageCallback(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, Action callback)
    {
        SendMessage(hWnd, msg, wParam, lParam);
        callback();
        return true;
    }

    public uint RegisterWindowMessage(string name)
    {
        if (!_registeredMessages.TryGetValue(name, out var id))
        {
            id = _nextRegisteredMessage++;
            _registeredMessages[name] = id;
        }

        return id;
    }

    /// <summary>
    /// Drain and dispatch all pending synthetic WinForms messages.
    /// Called from Silk.NET's Update callback.
    /// </summary>
    internal void ProcessPendingMessages()
    {
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.MessageDispatch,
            "ProcessPendingMessages");

        // Process a bounded batch to avoid blocking the render loop
        int maxMessages = 64;
        while (maxMessages-- > 0 && _messageQueue.TryDequeue(out var im))
        {
            var msg = im.ToMSG();
            if (msg.message == WM_QUIT)
            {
                // Close the Silk.NET window, which causes Run() to return
                if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
                {
                    foreach (var state in windowInterop.GetAllWindows())
                    {
                        state.SilkWindow?.Close();
                    }
                }

                return;
            }

            // Skip paint messages — the Render callback is the sole painter.
            // Processing WM_PAINT here would race with the GPU surface.
            if (msg.message is WM_PAINT or WM_ERASEBKGND)
            {
                continue;
            }

            // Dispatch through WinForms NativeWindow WndProc
            NativeWindow.DispatchMessageDirect(msg.hwnd, msg.message, msg.wParam, msg.lParam, out _);
        }
    }

    private static bool TryHandleToolTipMessage(uint msg, out LRESULT result)
    {
        result = (LRESULT)0;

        switch (msg)
        {
            case PInvoke.TTM_ADDTOOLW:
            case PInvoke.TTM_SETTOOLINFOW:
            case PInvoke.TTM_UPDATETIPTEXTW:
            case PInvoke.TTM_ADJUSTRECT:
                result = (LRESULT)1;
                return true;

            case PInvoke.TTM_ACTIVATE:
            case PInvoke.TTM_DELTOOLW:
            case PInvoke.TTM_GETBUBBLESIZE:
            case PInvoke.TTM_GETCURRENTTOOLW:
            case PInvoke.TTM_SETDELAYTIME:
            case PInvoke.TTM_SETMAXTIPWIDTH:
            case PInvoke.TTM_SETTIPBKCOLOR:
            case PInvoke.TTM_SETTIPTEXTCOLOR:
            case PInvoke.TTM_SETTITLEW:
            case PInvoke.TTM_TRACKACTIVATE:
            case PInvoke.TTM_TRACKPOSITION:
            case PInvoke.TTM_UPDATE:
                return true;

            case PInvoke.TTM_GETDELAYTIME:
                result = (LRESULT)ToolTip.DefaultDelay;
                return true;

            case PInvoke.TTM_GETTOOLINFOW:
                result = (LRESULT)1;
                return true;

            default:
                return false;
        }
    }

    private unsafe bool TryHandleListViewMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, out LRESULT result)
    {
        result = (LRESULT)0;
        if (Control.FromHandle(hWnd) is not ListView)
        {
            return false;
        }

        switch (msg)
        {
            case PInvoke.LVM_SETTILEVIEWINFO:
            {
                if ((nint)lParam == 0)
                {
                    return true;
                }

                LVTILEVIEWINFO* info = (LVTILEVIEWINFO*)(nint)lParam;
                _listViewTileViews.TryGetValue(hWnd, out ListViewTileViewState state);
                if (info->dwMask.HasFlag(LVTILEVIEWINFO_MASK.LVTVIM_TILESIZE))
                {
                    state = state with { TileSize = info->sizeTile };
                }

                if (info->dwMask.HasFlag(LVTILEVIEWINFO_MASK.LVTVIM_COLUMNS))
                {
                    state = state with { ColumnCount = info->cLines };
                }

                _listViewTileViews[hWnd] = state;
                result = (LRESULT)1;
                return true;
            }

            case PInvoke.LVM_GETTILEVIEWINFO:
            {
                if ((nint)lParam == 0)
                {
                    return true;
                }

                LVTILEVIEWINFO* info = (LVTILEVIEWINFO*)(nint)lParam;
                _listViewTileViews.TryGetValue(hWnd, out ListViewTileViewState state);
                if (info->dwMask.HasFlag(LVTILEVIEWINFO_MASK.LVTVIM_TILESIZE))
                {
                    info->sizeTile = state.TileSize;
                }

                if (info->dwMask.HasFlag(LVTILEVIEWINFO_MASK.LVTVIM_COLUMNS))
                {
                    info->cLines = state.ColumnCount;
                }

                result = (LRESULT)1;
                return true;
            }

            case PInvoke.LVM_SETCOLUMNW:
            {
                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                int columnIndex = (int)wParam;
                result = columnIndex >= 0 && columnIndex < listView.Columns.Count
                    ? (LRESULT)1
                    : (LRESULT)0;
                return true;
            }

            case PInvoke.LVM_GETITEMRECT:
            {
                if ((nint)lParam == 0)
                {
                    return true;
                }

                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                int itemIndex = (int)wParam;
                if (itemIndex < 0 || itemIndex >= listView.Items.Count)
                {
                    result = (LRESULT)0;
                    return true;
                }

                RECT* bounds = (RECT*)(nint)lParam;
                SetRect(bounds, GetListViewItemBounds(listView, itemIndex));
                result = (LRESULT)1;
                return true;
            }

            case PInvoke.LVM_GETSUBITEMRECT:
            {
                if ((nint)lParam == 0)
                {
                    return true;
                }

                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                int itemIndex = (int)wParam;
                RECT* bounds = (RECT*)(nint)lParam;
                if (!IsValidListViewSubItemRequest(listView, itemIndex, bounds->top))
                {
                    result = (LRESULT)0;
                    return true;
                }

                Rectangle subItemBounds = GetListViewSubItemBounds(listView, itemIndex, bounds->top);
                if (subItemBounds.IsEmpty)
                {
                    SetRect(bounds, Rectangle.Empty);
                    result = (LRESULT)1;
                    return true;
                }

                SetRect(bounds, subItemBounds);
                result = (LRESULT)1;
                return true;
            }

            case PInvoke.LVM_HITTEST:
            case PInvoke.LVM_SUBITEMHITTEST:
            {
                if ((nint)lParam == 0)
                {
                    result = (LRESULT)(-1);
                    return true;
                }

                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                LVHITTESTINFO* hitTestInfo = (LVHITTESTINFO*)(nint)lParam;
                int itemIndex = HitTestListView(listView, hitTestInfo->pt, out int subItemIndex);
                hitTestInfo->iItem = itemIndex;
                hitTestInfo->iSubItem = itemIndex >= 0 ? subItemIndex : 0;
                hitTestInfo->flags = itemIndex >= 0
                    ? LVHITTESTINFO_FLAGS.LVHT_ONITEMLABEL
                    : LVHITTESTINFO_FLAGS.LVHT_NOWHERE;
                result = (LRESULT)itemIndex;
                return true;
            }

            case PInvoke.LVM_GETITEMSTATE:
            {
                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                int itemIndex = (int)wParam;
                if (itemIndex < 0 || itemIndex >= listView.Items.Count)
                {
                    result = (LRESULT)0;
                    return true;
                }

                LIST_VIEW_ITEM_STATE_FLAGS mask = (LIST_VIEW_ITEM_STATE_FLAGS)(uint)(nint)lParam;
                result = (LRESULT)(nint)(uint)(GetListViewItemState(listView, itemIndex) & mask);
                return true;
            }

            case PInvoke.LVM_SETITEMSTATE:
            {
                if ((nint)lParam == 0)
                {
                    return true;
                }

                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                LVITEMW* itemState = (LVITEMW*)(nint)lParam;
                int itemIndex = (int)wParam;
                SetListViewItemState(listView, itemIndex, itemState->state, itemState->stateMask);
                result = (LRESULT)1;
                return true;
            }

            case PInvoke.LVM_GETSELECTEDCOUNT:
            {
                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                result = (LRESULT)GetSelectedListViewItemCount(listView);
                return true;
            }

            case PInvoke.LVM_GETNEXTITEM:
            {
                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                int startIndex = (int)wParam;
                int flags = (int)(nint)lParam;
                result = (LRESULT)GetNextListViewItem(listView, startIndex, flags);
                return true;
            }

            case PInvoke.WM_KEYDOWN:
            {
                ListView listView = (ListView)Control.FromHandle(hWnd)!;
                if (TryMoveListViewSelection(hWnd, listView, (VIRTUAL_KEY)(uint)(nint)wParam))
                {
                    result = (LRESULT)0;
                    return true;
                }

                break;
            }
        }

        return false;
    }

    private static LIST_VIEW_ITEM_STATE_FLAGS GetListViewItemState(ListView listView, int itemIndex)
    {
        ListViewItem item = listView.Items[itemIndex];
        LIST_VIEW_ITEM_STATE_FLAGS state = item.RawStateImageIndex;
        if (item.StateSelected)
        {
            state |= LIST_VIEW_ITEM_STATE_FLAGS.LVIS_SELECTED | LIST_VIEW_ITEM_STATE_FLAGS.LVIS_FOCUSED;
        }

        if (ReferenceEquals(listView._selectedItem, item))
        {
            state |= LIST_VIEW_ITEM_STATE_FLAGS.LVIS_FOCUSED;
        }

        return state;
    }

    private static void SetListViewItemState(
        ListView listView,
        int itemIndex,
        LIST_VIEW_ITEM_STATE_FLAGS state,
        LIST_VIEW_ITEM_STATE_FLAGS stateMask)
    {
        int start = itemIndex == -1 ? 0 : itemIndex;
        int end = itemIndex == -1 ? listView.Items.Count : Math.Min(itemIndex + 1, listView.Items.Count);
        for (int i = start; i < end; i++)
        {
            ListViewItem item = listView.Items[i];
            if (stateMask.HasFlag(LIST_VIEW_ITEM_STATE_FLAGS.LVIS_SELECTED))
            {
                item.StateSelected = state.HasFlag(LIST_VIEW_ITEM_STATE_FLAGS.LVIS_SELECTED);
                if (item.StateSelected)
                {
                    listView._selectedItem = item;
                }
                else if (ReferenceEquals(listView._selectedItem, item))
                {
                    listView._selectedItem = null;
                }
            }
        }
    }

    private static int GetSelectedListViewItemCount(ListView listView)
    {
        int count = 0;
        foreach (ListViewItem item in listView.Items)
        {
            if (item.StateSelected)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetNextListViewItem(ListView listView, int startIndex, int flags)
    {
        bool selected = (flags & PInvoke.LVNI_SELECTED) != 0;
        bool focused = (flags & PInvoke.LVNI_FOCUSED) != 0;
        for (int i = Math.Max(-1, startIndex) + 1; i < listView.Items.Count; i++)
        {
            ListViewItem item = listView.Items[i];
            if ((selected && item.StateSelected) || (focused && ReferenceEquals(listView._selectedItem, item)))
            {
                return i;
            }
        }

        return -1;
    }

    private bool TryMoveListViewSelection(HWND hWnd, ListView listView, VIRTUAL_KEY key)
    {
        if (listView.Items.Count == 0)
        {
            return false;
        }

        if (TryApplyListViewGroupKey(hWnd, listView, key))
        {
            return true;
        }

        if (key is not (VIRTUAL_KEY.VK_LEFT or VIRTUAL_KEY.VK_RIGHT))
        {
            return false;
        }

        int selectedIndex = listView._selectedItem is null ? -1 : listView.ManagedIndexOf(listView._selectedItem);
        if (selectedIndex < 0)
        {
            for (int i = 0; i < listView.Items.Count; i++)
            {
                if (listView.Items[i].StateSelected)
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        int nextIndex = key == VIRTUAL_KEY.VK_RIGHT
            ? Math.Min(listView.Items.Count - 1, selectedIndex + 1)
            : Math.Max(0, selectedIndex - 1);
        if (nextIndex < 0 || nextIndex == selectedIndex)
        {
            return true;
        }

        for (int i = 0; i < listView.Items.Count; i++)
        {
            listView.Items[i].StateSelected = i == nextIndex;
        }

        listView._selectedItem = listView.Items[nextIndex];
        _listViewGroupHeaderFocus.Remove(hWnd);
        listView.Invalidate();
        return true;
    }

    private bool TryApplyListViewGroupKey(HWND hWnd, ListView listView, VIRTUAL_KEY key)
    {
        if (!listView.GroupsDisplayed || listView.Groups.Count == 0)
        {
            return false;
        }

        ListViewGroup? group = listView._selectedItem?.Group ?? listView.Groups[0];
        if (group is null || group.CollapsedState == ListViewGroupCollapsedState.Default)
        {
            return false;
        }

        if (key == VIRTUAL_KEY.VK_UP)
        {
            foreach (ListViewItem item in listView.Items)
            {
                item.StateSelected = ReferenceEquals(item.Group, group);
            }

            _listViewGroupHeaderFocus.Add(hWnd);
            listView.Invalidate();
            return true;
        }

        if (!_listViewGroupHeaderFocus.Contains(hWnd) || key is not (VIRTUAL_KEY.VK_LEFT or VIRTUAL_KEY.VK_RIGHT))
        {
            return false;
        }

        ListViewGroupCollapsedState nextState = key == VIRTUAL_KEY.VK_LEFT
            ? ListViewGroupCollapsedState.Collapsed
            : ListViewGroupCollapsedState.Expanded;
        if (group.CollapsedState != nextState)
        {
            group.SetCollapsedStateInternal(nextState);
            listView.RaiseGroupCollapsedStateChanged(new ListViewGroupEventArgs(listView.Groups.IndexOf(group)));
        }

        return true;
    }

    private static bool IsValidListViewSubItemRequest(ListView listView, int itemIndex, int subItemIndex)
    {
        if (itemIndex < 0 || itemIndex >= listView.Items.Count || subItemIndex < 0)
        {
            return false;
        }

        return listView.View switch
        {
            View.Tile => subItemIndex < listView.Items[itemIndex].SubItems.Count,
            View.Details => subItemIndex < listView.Columns.Count,
            _ => false
        };
    }

    private Rectangle GetListViewItemBounds(ListView listView, int itemIndex)
    {
        if (listView.View == View.Tile)
        {
            Size tileSize = GetListViewTileSize(listView);
            return new Rectangle(0, itemIndex * tileSize.Height, tileSize.Width, tileSize.Height);
        }

        int height = GetListViewRowHeight(listView);
        return new Rectangle(0, itemIndex * height, Math.Max(1, listView.ClientSize.Width), height);
    }

    private Rectangle GetListViewSubItemBounds(ListView listView, int itemIndex, int subItemIndex)
    {
        if (itemIndex < 0 || itemIndex >= listView.Items.Count || subItemIndex < 0)
        {
            return Rectangle.Empty;
        }

        ListViewItem item = listView.Items[itemIndex];
        if (listView.View == View.Tile)
        {
            if (subItemIndex >= item.SubItems.Count
                || subItemIndex >= listView.Columns.Count
                || !IsVisibleTileSubItem(listView, subItemIndex))
            {
                return Rectangle.Empty;
            }

            Size tileSize = GetListViewTileSize(listView);
            int lineHeight = GetListViewTileLineHeight();
            int y = itemIndex * tileSize.Height + (subItemIndex == 0 ? 0 : lineHeight + ((subItemIndex - 1) * lineHeight));
            return new Rectangle(0, y, tileSize.Width, lineHeight);
        }

        if (listView.View == View.Details)
        {
            if (subItemIndex >= listView.Columns.Count)
            {
                return Rectangle.Empty;
            }

            int x = 0;
            for (int i = 0; i < subItemIndex; i++)
            {
                x += listView.Columns[i].Width;
            }

            int height = GetListViewRowHeight(listView);
            return new Rectangle(x, itemIndex * height, listView.Columns[subItemIndex].Width, height);
        }

        return Rectangle.Empty;
    }

    private static bool IsVisibleTileSubItem(ListView listView, int subItemIndex)
    {
        if (subItemIndex == 0)
        {
            return true;
        }

        Size tileSize = GetListViewTileSize(listView);
        int visibleSubItemCount = Math.Max(0, (tileSize.Height - GetListViewTileLineHeight()) / GetListViewTileLineHeight());
        return subItemIndex <= visibleSubItemCount;
    }

    private int HitTestListView(ListView listView, Point point, out int subItemIndex)
    {
        subItemIndex = 0;
        for (int itemIndex = 0; itemIndex < listView.Items.Count; itemIndex++)
        {
            Rectangle itemBounds = GetListViewItemBounds(listView, itemIndex);
            if (!itemBounds.Contains(point))
            {
                continue;
            }

            int subItemCount = listView.View == View.Details
                ? listView.Columns.Count
                : Math.Min(listView.Columns.Count, listView.Items[itemIndex].SubItems.Count);
            for (int i = 0; i < subItemCount; i++)
            {
                if (GetListViewSubItemBounds(listView, itemIndex, i).Contains(point))
                {
                    subItemIndex = i;
                    return itemIndex;
                }
            }

            return itemIndex;
        }

        return -1;
    }

    private static Size GetListViewTileSize(ListView listView)
        => listView.TileSize.IsEmpty ? new Size(Math.Max(1, listView.ClientSize.Width), 48) : listView.TileSize;

    private static int GetListViewTileLineHeight() => 16;

    private static int GetListViewRowHeight(ListView listView) => Math.Max(18, listView.Font.Height + 4);

    private static unsafe void SetRect(RECT* target, Rectangle source)
    {
        target->left = source.Left;
        target->top = source.Top;
        target->right = source.Right;
        target->bottom = source.Bottom;
    }

    private unsafe bool TryHandleTextBoxMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, out LRESULT result)
    {
        result = (LRESULT)0;
        if (Control.FromHandle(hWnd) is not TextBoxBase textBox)
        {
            return false;
        }

        switch (msg)
        {
            case EM_GETSEL:
            {
                GetTextSelection(hWnd, textBox, out int start, out int length);
                int end = Math.Min(textBox.Text.Length, start + length);
                if ((nint)wParam != 0)
                {
                    *(int*)(nint)wParam = start;
                }

                if ((nint)lParam != 0)
                {
                    *(int*)(nint)lParam = end;
                }

                uint packed = unchecked((uint)((start & 0xFFFF) | ((end & 0xFFFF) << 16)));
                result = (LRESULT)(nint)packed;
                return true;
            }

            case EM_SETSEL:
            {
                int textLength = textBox.Text.Length;
                int start = (int)(nint)wParam;
                int end = (int)(nint)lParam;
                if (start < 0)
                {
                    start = textLength;
                }

                if (end < 0)
                {
                    end = textLength;
                }

                start = Math.Clamp(start, 0, textLength);
                end = Math.Clamp(end, 0, textLength);
                if (end < start)
                {
                    (start, end) = (end, start);
                }

                _textSelections[hWnd] = new TextSelectionState(start, end - start);
                return true;
            }

            case EM_REPLACESEL:
            {
                if (textBox.ReadOnly)
                {
                    return true;
                }

                string replacement = (nint)lParam == 0 ? string.Empty : new string((char*)(nint)lParam);
                GetTextSelection(hWnd, textBox, out int start, out int length);
                string oldText = textBox.Text;
                start = Math.Clamp(start, 0, oldText.Length);
                length = Math.Clamp(length, 0, oldText.Length - start);

                int allowed = Math.Max(0, textBox.MaxLength - (oldText.Length - length));
                if (replacement.Length > allowed)
                {
                    replacement = replacement[..allowed];
                }

                textBox.Text = string.Concat(oldText.AsSpan(0, start), replacement, oldText.AsSpan(start + length));
                _textSelections[hWnd] = new TextSelectionState(start + replacement.Length, 0);
                return true;
            }

            case EM_LIMITTEXT:
            case EM_SETMODIFY:
            case EM_SETREADONLY:
                return true;
        }

        return false;
    }

    private void GetTextSelection(HWND hWnd, TextBoxBase textBox, out int start, out int length)
    {
        if (!_textSelections.TryGetValue(hWnd, out TextSelectionState selection))
        {
            selection = new TextSelectionState(0, 0);
        }

        int textLength = textBox.Text.Length;
        start = Math.Clamp(selection.Start, 0, textLength);
        length = Math.Clamp(selection.Length, 0, textLength - start);
    }

    private readonly record struct TextSelectionState(int Start, int Length);

    private readonly record struct ListViewTileViewState(Size TileSize, int ColumnCount);
}

/// <summary>
/// Internal message representation for the Impeller message queue.
/// </summary>
internal readonly record struct ImpellerMessage(HWND HWnd, uint Msg, WPARAM WParam, LPARAM LParam)
{
    public MSG ToMSG() => new()
    {
        hwnd = HWnd,
        message = Msg,
        wParam = WParam,
        lParam = LParam,
    };
}

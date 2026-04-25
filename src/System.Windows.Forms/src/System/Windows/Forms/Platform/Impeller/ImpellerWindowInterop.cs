// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

using Silk.NET.Windowing;
using Silk.NET.Maths;

/// <summary>
/// Impeller window management — manages virtual windows backed by Impeller surfaces.
/// Each "window" is an Impeller rendering target with its own display list.
/// </summary>
internal sealed class ImpellerWindowInterop : IWindowInterop
{
    // Internal window registry: HWND -> ImpellerSurface mapping
    private static long s_nextHandle = 0x10000;
    private HWND _activeWindow;
    private readonly Dictionary<nint, ImpellerWindowState> _windows = [];

    public void PumpEvents()
    {
        foreach (var state in _windows.Values)
        {
            if (state.SilkWindow is object)
            {
                state.SilkWindow.DoEvents();
                state.SilkWindow.DoUpdate();
                state.SilkWindow.DoRender();
            }
        }
    }

    public HWND CreateWindowEx(
        WINDOW_EX_STYLE dwExStyle, string? lpClassName, string? lpWindowName,
        WINDOW_STYLE dwStyle, int x, int y, int nWidth, int nHeight,
        HWND hWndParent, HMENU hMenu, HINSTANCE hInstance, object? lpParam)
    {
        var handle = (HWND)(nint)System.Threading.Interlocked.Increment(ref s_nextHandle);
        _windows[handle] = new ImpellerWindowState
        {
            Handle = handle,
            ClassName = lpClassName,
            Title = lpWindowName ?? string.Empty,
            Style = dwStyle,
            ExStyle = dwExStyle,
            X = x,
            Y = y,
            Width = nWidth,
            Height = nHeight,
            Parent = hWndParent,
            Visible = false,
            // Sentinel default WndProc — the framework expects a non-null prior
            // WndProc when subclassing via AssignHandle/SetWindowLong.
            WndProc = 0x1,
        };

        if (hWndParent == HWND.Null)
        {
            var options = WindowOptions.DefaultVulkan;
            options.Title = lpWindowName ?? string.Empty;
            options.Position = new Vector2D<int>(x, y);
            options.Size = new Vector2D<int>(nWidth > 0 ? nWidth : 800, nHeight > 0 ? nHeight : 600);
            options.IsVisible = false;
            
            var silkWindow = Window.Create(options);
            
            silkWindow.Resize += (size) =>
            {
                NativeWindow.DispatchMessageDirect(
                    handle, (uint)PInvoke.WM_SIZE, (WPARAM)0, (LPARAM)(nint)((size.Y << 16) | (size.X & 0xFFFF)), out _);
            };
            
            silkWindow.Move += (pos) =>
            {
                NativeWindow.DispatchMessageDirect(
                    handle, (uint)PInvoke.WM_MOVE, (WPARAM)0, (LPARAM)(nint)((pos.Y << 16) | (pos.X & 0xFFFF)), out _);
            };
            
            silkWindow.Closing += () =>
            {
                NativeWindow.DispatchMessageDirect(
                    handle, (uint)PInvoke.WM_CLOSE, (WPARAM)0, (LPARAM)0, out _);
            };
            
            bool renderReady = false;

            silkWindow.Render += (delta) =>
            {
                if (!renderReady) return;

                // Flutter/wxWidgets pattern: the Render event is the ONLY place
                // where the GPU context is current.  We own the frame lifecycle here.
                int w = silkWindow.Size.X;
                int h = silkWindow.Size.Y;
                if (w <= 0 || h <= 0) return;

                // Keep stored dimensions in sync
                if (_windows.TryGetValue(handle, out var ws))
                {
                    ws.Width = w;
                    ws.Height = h;
                }

                // Guard: catch managed exceptions during backend init
                System.Drawing.Graphics? g;
                try
                {
                    g = System.Drawing.Graphics.FromHwndInternal((IntPtr)handle);
                }
                catch
                {
                    return; // Backend not ready yet
                }

                using (g)
                {
                    if (!g.BeginFrame(w, h))
                        return;

                    try
                    {
                        var control = Control.FromHandle(handle);
                        if (control is not null)
                        {
                            if (s_firstPaint)
                            {
                                Console.Error.WriteLine($"[Render] Form ClientSize={control.ClientSize} Size={control.Size}");
                            }

                            // (1) Clear entire frame to magenta — proof the pipeline works
                            g.Clear(System.Drawing.Color.Magenta);

                            // (2) Paint child controls with distinct debug colors
                            var debugColors = new[]
                            {
                                System.Drawing.Color.DodgerBlue,
                                System.Drawing.Color.ForestGreen,
                                System.Drawing.Color.Orange,
                                System.Drawing.Color.Crimson,
                                System.Drawing.Color.MediumPurple,
                                System.Drawing.Color.Teal,
                            };

                            int colorIdx = 0;
                            for (int i = control.Controls.Count - 1; i >= 0; i--)
                            {
                                var child = control.Controls[i];
                                if (!child.Visible || child.Width <= 0 || child.Height <= 0)
                                    continue;

                                var color = debugColors[colorIdx % debugColors.Length];
                                colorIdx++;

                                using var brush = new System.Drawing.SolidBrush(color);
                                var rect = new System.Drawing.Rectangle(child.Left, child.Top, child.Width, child.Height);
                                g.FillRectangle(brush, rect);

                                if (s_firstPaint)
                                {
                                    Console.Error.WriteLine($"  [{child.GetType().Name}] {rect} → {color.Name}");
                                }
                            }

                            s_firstPaint = false;
                        }
                    }
                    finally
                    {
                        g.EndFrame(w, h);
                    }
                }
            };
            
            silkWindow.Initialize();
            _windows[handle].SilkWindow = silkWindow;

            // Only allow rendering AFTER the window is fully initialized
            renderReady = true;
        }

        return handle;
    }

    /// <summary>
    /// Recursively paint a control and all its children using the shared
    /// Impeller-backed Graphics object from the Render event handler.
    /// </summary>
    private static bool s_firstPaint = true;
    private static void PaintControlTree(Control control, System.Drawing.Graphics g, System.Drawing.Rectangle clipRect)
    {
        // Paint this control's background and foreground.
        // Wrap in try-catch so a single control's exception doesn't kill rendering.
        try
        {
            if (s_firstPaint)
            {
                Console.Error.WriteLine($"[Paint] {control.GetType().Name} @ ({control.Left},{control.Top}) {control.Width}x{control.Height} children={control.Controls.Count}");
                if (control.Controls.Count > 0)
                {
                    foreach (Control c in control.Controls)
                    {
                        Console.Error.WriteLine($"  child: {c.GetType().Name} Visible={c.Visible} {c.Width}x{c.Height} @ ({c.Left},{c.Top})");
                    }
                }
            }

            // Direct background fill — bypasses WinForms GDI complexity
            using var brush = new System.Drawing.SolidBrush(control.BackColor);
            g.FillRectangle(brush, clipRect);

            // Try the standard WinForms paint path too
            try
            {
                using var peArgs = new PaintEventArgs(g, clipRect);
                control.InvokePaintInternal(peArgs);
            }
            catch { }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Paint ERROR] {control.GetType().Name}: {ex.Message}");
        }

        // Recurse into children (bottom-to-top Z-order: last index = bottom-most = painted first)
        for (int i = control.Controls.Count - 1; i >= 0; i--)
        {
            var child = control.Controls[i];
            if (!child.Visible || child.Width <= 0 || child.Height <= 0)
                continue;

            var state = g.Save();

            // Translate to child's position and clip to its bounds
            g.TranslateTransform(child.Left, child.Top);
            var childClip = new System.Drawing.Rectangle(0, 0, child.Width, child.Height);
            g.IntersectClip(childClip);

            PaintControlTree(child, g, childClip);

            g.Restore(state);
        }
    }

    internal Silk.NET.Windowing.IWindow? GetSilkWindow(HWND hWnd)
    {
        if (_windows.TryGetValue(hWnd, out var state))
        {
            return state.SilkWindow;
        }

        return null;
    }

    internal ImpellerWindowState? GetWindowState(HWND hWnd)
    {
        _windows.TryGetValue(hWnd, out var state);
        return state;
    }

    public bool DestroyWindow(HWND hWnd)
    {
        if (_windows.TryGetValue(hWnd, out var state))
        {
            state.SilkWindow?.Dispose();
            _windows.Remove(hWnd);
            return true;
        }

        return false;
    }

    public ushort RegisterClass(in WNDCLASSW wc) => 1; // Always succeeds
    public bool UnregisterClass(string className, HINSTANCE hInstance) => true;

    public bool ShowWindow(HWND hWnd, SHOW_WINDOW_CMD nCmdShow)
    {
        if (_windows.TryGetValue(hWnd, out var state))
        {
            bool show = nCmdShow != SHOW_WINDOW_CMD.SW_HIDE;
            state.Visible = show;

            if (state.SilkWindow is object)
            {
                state.SilkWindow.IsVisible = show;
                if (nCmdShow is SHOW_WINDOW_CMD.SW_MAXIMIZE or SHOW_WINDOW_CMD.SW_SHOWMAXIMIZED)
                {
                    state.SilkWindow.WindowState = WindowState.Maximized;
                }
                else if (nCmdShow is SHOW_WINDOW_CMD.SW_MINIMIZE or SHOW_WINDOW_CMD.SW_SHOWMINIMIZED or SHOW_WINDOW_CMD.SW_SHOWMINNOACTIVE)
                {
                    state.SilkWindow.WindowState = WindowState.Minimized;
                }
                else if (nCmdShow is SHOW_WINDOW_CMD.SW_RESTORE or SHOW_WINDOW_CMD.SW_NORMAL or SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE or SHOW_WINDOW_CMD.SW_SHOW)
                {
                    state.SilkWindow.WindowState = WindowState.Normal;
                }
            }

            // wxWidgets pattern: dispatch WM_SHOWWINDOW so the managed Control
            // state (States.Visible) is updated, preventing recursion when
            // Form.CreateHandle sets Visible = true.
            NativeWindow.DispatchMessageDirect(
                hWnd,
                (uint)PInvoke.WM_SHOWWINDOW,
                (WPARAM)(nuint)(show ? 1u : 0u),
                (LPARAM)0,
                out _);

            return true;
        }

        return false;
    }

    public bool EnableWindow(HWND hWnd, bool bEnable) => SetFlag(hWnd, s => s.Enabled = bEnable);
    public bool IsWindow(HWND hWnd) => _windows.ContainsKey(hWnd);
    public bool IsWindowVisible(HWND hWnd) => _windows.TryGetValue(hWnd, out var s) && s.Visible;
    public bool IsWindowEnabled(HWND hWnd) => _windows.TryGetValue(hWnd, out var s) && s.Enabled;
    public HWND SetActiveWindow(HWND hWnd) { _activeWindow = hWnd; return hWnd; }
    public bool SetForegroundWindow(HWND hWnd) { _activeWindow = hWnd; return true; }
    public bool IsChild(HWND hWndParent, HWND hWnd) => _windows.TryGetValue(hWnd, out var s) && s.Parent == hWndParent;
    public HWND GetWindow(HWND hWnd, GET_WINDOW_CMD uCmd) => HWND.Null;
    public bool UpdateWindow(HWND hWnd) => true; // Impeller repaints on frame

    public bool MoveWindow(HWND hWnd, int x, int y, int w, int h, bool repaint)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            s.X = x;
            s.Y = y;
            s.Width = w;
            s.Height = h;
            
            if (s.SilkWindow is object)
            {
                s.SilkWindow.Position = new Vector2D<int>(x, y);
                s.SilkWindow.Size = new Vector2D<int>(w, h);
            }
            
            return true;
        }

        return false;
    }

    public unsafe bool SetWindowPos(HWND hWnd, HWND insertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS flags)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOMOVE))
            { s.X = x; s.Y = y; }
            if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOSIZE))
            { s.Width = cx; s.Height = cy; }
            
            if (s.SilkWindow is object)
            {
                if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOMOVE))
                    s.SilkWindow.Position = new Vector2D<int>(x, y);
                if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOSIZE))
                    s.SilkWindow.Size = new Vector2D<int>(cx, cy);
            }

            // In real Win32, SetWindowPos sends WM_WINDOWPOSCHANGED which
            // triggers WmWindowPosChanged → UpdateBounds → layout cascade.
            // We must simulate this or _clientWidth/_clientHeight stay stale.
            WINDOWPOS wp = new()
            {
                hwnd = hWnd,
                hwndInsertAfter = insertAfter,
                x = x,
                y = y,
                cx = cx,
                cy = cy,
                flags = flags,
            };

            WINDOWPOS* pWp = &wp;
            LPARAM lParam = (LPARAM)(nint)pWp;
            NativeWindow.DispatchMessageDirect(
                hWnd, PInvoke.WM_WINDOWPOSCHANGED, (WPARAM)0, lParam, out _);

            return true;
        }

        return false;
    }

    public bool GetWindowRect(HWND hWnd, out RECT rect)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            rect = new RECT(s.X, s.Y, s.X + s.Width, s.Y + s.Height);
            return true;
        }

        rect = default;
        return false;
    }

    public bool GetClientRect(HWND hWnd, out RECT rect)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            rect = new RECT(0, 0, s.Width, s.Height);
            return true;
        }

        rect = default;
        return false;
    }

    public bool AdjustWindowRectEx(ref RECT rect, WINDOW_STYLE style, bool menu, WINDOW_EX_STYLE exStyle)
        => true; // No chrome adjustment in Impeller

    public int MapWindowPoints(HWND from, HWND to, ref RECT rect)
    {
        // TODO: implement coordinate transformation between windows
        return 0;
    }

    public int MapWindowPoints(HWND from, HWND to, ref System.Drawing.Point pts, uint count) => 0;
    public bool ScreenToClient(HWND hWnd, ref System.Drawing.Point pt) => true;
    public bool ClientToScreen(HWND hWnd, ref System.Drawing.Point pt) => true;

    public nint GetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX index)
    {
        if (!_windows.TryGetValue(hWnd, out var s))
            return 0;
        return index switch
        {
            WINDOW_LONG_PTR_INDEX.GWL_STYLE => (nint)(int)s.Style,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE => (nint)(int)s.ExStyle,
            WINDOW_LONG_PTR_INDEX.GWL_WNDPROC => s.WndProc,
            WINDOW_LONG_PTR_INDEX.GWL_ID => s.Id,
            _ => 0,
        };
    }

    public nint SetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX index, nint value)
    {
        if (!_windows.TryGetValue(hWnd, out var s))
            return 0;
        nint old = GetWindowLong(hWnd, index);
        switch (index)
        {
            case WINDOW_LONG_PTR_INDEX.GWL_STYLE:
                s.Style = (WINDOW_STYLE)(int)value;
                break;
            case WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE:
                s.ExStyle = (WINDOW_EX_STYLE)(int)value;
                break;
            case WINDOW_LONG_PTR_INDEX.GWL_WNDPROC:
                s.WndProc = value;
                break;
            case WINDOW_LONG_PTR_INDEX.GWL_ID:
                s.Id = value;
                break;
        }

        return old;
    }

    public bool SetWindowText(HWND hWnd, string text)
    {
        if (SetFlag(hWnd, s => s.Title = text))
        {
            if (_windows.TryGetValue(hWnd, out var state) && state.SilkWindow is object)
            {
                state.SilkWindow.Title = text;
            }

            return true;
        }

        return false;
    }

    public int GetWindowText(HWND hWnd, Span<char> buf)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            var len = Math.Min(s.Title.Length, buf.Length - 1);
            s.Title.AsSpan(0, len).CopyTo(buf);
            buf[len] = '\0';
            return len;
        }

        return 0;
    }

    public int GetWindowTextLength(HWND hWnd)
        => _windows.TryGetValue(hWnd, out var s) ? s.Title.Length : 0;

    public HWND GetParent(HWND hWnd)
        => _windows.TryGetValue(hWnd, out var s) ? s.Parent : HWND.Null;

    public HWND SetParent(HWND child, HWND newParent)
    {
        if (_windows.TryGetValue(child, out var s))
        {
            var old = s.Parent;
            s.Parent = newParent;
            return old;
        }

        return HWND.Null;
    }

    public HWND GetAncestor(HWND hwnd, GET_ANCESTOR_FLAGS flags) => GetParent(hwnd);
    public HWND GetDesktopWindow() => (HWND)(nint)0x10000;
    public HWND WindowFromPoint(System.Drawing.Point pt) => HWND.Null; // TODO: hit-test
    public HWND ChildWindowFromPointEx(HWND parent, System.Drawing.Point pt, uint flags) => HWND.Null;
    public bool EnumChildWindows(HWND parent, Func<HWND, bool> callback) => true;
    public bool EnumWindows(Func<HWND, bool> callback) => true;

    public bool InvalidateRect(HWND hWnd, RECT? rect, bool erase)
    {
        // In real Win32, InvalidateRect marks a region as dirty. The OS sends
        // WM_PAINT later during the message loop. Our Silk.NET Render event
        // fires continuously and handles painting, so no immediate dispatch needed.
        return true;
    }

    public bool RedrawWindow(HWND hWnd, RECT? rect, HRGN rgn, REDRAW_WINDOW_FLAGS flags) => true;
    public bool ValidateRect(HWND hWnd, RECT? rect) => true;

    public LRESULT DefWindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        => (LRESULT)0; // Default: message handled

    public unsafe LRESULT CallWindowProc(void* prev, HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        => (LRESULT)0;

    public bool PostMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        => true; // TODO: enqueue to internal message queue

    // --- Internal helpers -----------------------------------------------

    private bool SetFlag(HWND hWnd, Action<ImpellerWindowState> setter)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        { setter(s); return true; }
        return false;
    }
}

/// <summary>
/// Internal state for an Impeller-managed virtual window.
/// </summary>
internal sealed class ImpellerWindowState
{
    public HWND Handle;
    public string? ClassName;
    public string Title = string.Empty;
    public WINDOW_STYLE Style;
    public WINDOW_EX_STYLE ExStyle;
    public int X, Y, Width, Height;
    public HWND Parent;
    public bool Visible;
    public bool Enabled = true;
    public nint WndProc;
    public nint Id;
    public IWindow? SilkWindow;
}



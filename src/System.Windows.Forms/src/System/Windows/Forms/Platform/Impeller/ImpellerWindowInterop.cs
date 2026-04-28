// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

/// <summary>
/// Impeller window management — manages virtual windows backed by Impeller surfaces.
/// Each "window" is an Impeller rendering target with its own display list.
/// </summary>
internal sealed class ImpellerWindowInterop : IWindowInterop
{
    private enum HiDpiMode
    {
        Auto,
        Off,
        On,
    }

    // Managed Win32 message IDs used by the Impeller PAL path.
    // Keep these local so this interop never touches Windows.Win32.PInvoke.
    private const uint WM_MOVE = 0x0003;
    private const uint WM_SIZE = 0x0005;
    private const uint WM_SHOWWINDOW = 0x0018;
    private const uint WM_WINDOWPOSCHANGED = 0x0047;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_CHAR = 0x0102;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const uint WM_MOUSEWHEEL = 0x020A;
    private const uint WM_MOUSEHWHEEL = 0x020E;

    // Internal window registry: HWND -> ImpellerSurface mapping
    private static long s_nextHandle = 0x10000;
    private static readonly string? s_traceFile = Environment.GetEnvironmentVariable("WINFORMSX_TRACE_FILE");
    private static readonly HiDpiMode s_hiDpiMode = ParseHiDpiMode();
    private static readonly float s_hiDpiScaleOverride = ParseHiDpiScaleOverride();
    private HWND _activeWindow;
    private readonly Dictionary<nint, ImpellerWindowState> _windows = [];

    internal IEnumerable<ImpellerWindowState> GetAllWindows() => _windows.Values;

    /// <summary>
    /// Run Silk.NET's native event loop. This properly handles window
    /// move, resize, close, and all native OS interactions.
    /// WinForms synthetic messages are processed from the Update callback.
    /// </summary>
    public void RunMainLoop()
    {
        // Find the top-level Silk.NET window
        IWindow? mainWindow = null;
        foreach (var state in _windows.Values)
        {
            if (state.SilkWindow is object)
            {
                mainWindow = state.SilkWindow;
                break;
            }
        }

        if (mainWindow is null)
            return;

        // Process WinForms synthetic messages on each Update tick
        mainWindow.Update += (delta) =>
        {
            if (PlatformApi.Message is ImpellerMessageInterop msgInterop)
            {
                try
                {
                    msgInterop.ProcessPendingMessages();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CRASH in ProcessPendingMessages] {ex}");
                    Application.OnThreadException(ex);
                }
            }
        };

        // Let Silk.NET own the event loop — handles move, resize, close natively
        mainWindow.Run();
    }

    // Legacy pump for any non-main-loop callers (e.g. DoEvents)
    public void PumpEvents()
    {
        foreach (var state in _windows.Values)
        {
            if (state.SilkWindow is object)
            {
                try
                {
                    state.SilkWindow.DoEvents();
                    state.SilkWindow.DoUpdate();
                    state.SilkWindow.DoRender();
                }
                catch (Exception ex)
                {
                    Application.OnThreadException(ex);
                }
            }
        }
    }

    public HWND CreateWindowEx(
        WINDOW_EX_STYLE dwExStyle, string? lpClassName, string? lpWindowName,
        WINDOW_STYLE dwStyle, int x, int y, int nWidth, int nHeight,
        HWND hWndParent, HMENU hMenu, HINSTANCE hInstance, object? lpParam)
    {
        int effectiveWidth = nWidth > 0 ? nWidth : 800;
        int effectiveHeight = nHeight > 0 ? nHeight : 600;

        // Some create paths hand us title-bar-sized bounds (e.g., 136x39) for top-level
        // forms. Clamp to a sane minimum so the render surface is usable.
        if (hWndParent == HWND.Null)
        {
            if (effectiveWidth < 640)
            {
                effectiveWidth = 900;
            }

            if (effectiveHeight < 360)
            {
                effectiveHeight = 600;
            }
        }

        var handle = (HWND)(nint)Interlocked.Increment(ref s_nextHandle);
        _windows[handle] = new ImpellerWindowState
        {
            Handle = handle,
            ClassName = lpClassName,
            Title = lpWindowName ?? string.Empty,
            Style = dwStyle,
            ExStyle = dwExStyle,
            X = x,
            Y = y,
            Width = effectiveWidth,
            Height = effectiveHeight,
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
            // CW_USEDEFAULT (0x80000000) yields extreme negatives — default to (100,100)
            int posX = (x < 0 || x > 4000) ? 100 : x;
            int posY = (y < 0 || y > 4000) ? 100 : y;
            options.Position = new Vector2D<int>(posX, posY);
            options.Size = new Vector2D<int>(effectiveWidth, effectiveHeight);
            options.IsVisible = false;
            if (((WINDOW_STYLE)dwStyle).HasFlag(WINDOW_STYLE.WS_POPUP))
            {
                options.WindowBorder = WindowBorder.Hidden;
            }
            else
            {
                options.WindowBorder = WindowBorder.Resizable;
            }

            var silkWindow = Window.Create(options);
            bool renderReady = false;

            silkWindow.Resize += (size) =>
            {
                if (!renderReady)
                    return;
                MarkDirty(handle);
                PostMessageToControl(handle, handle, WM_SIZE, (WPARAM)0, (LPARAM)(nint)((size.Y << 16) | (size.X & 0xFFFF)));
            };

            silkWindow.Move += (pos) =>
            {
                if (!renderReady)
                    return;
                MarkDirty(handle);
                PostMessageToControl(handle, handle, WM_MOVE, (WPARAM)0, (LPARAM)(nint)((pos.Y << 16) | (pos.X & 0xFFFF)));
            };

            silkWindow.Closing += () =>
            {
                // Silk.NET's Run() will return naturally when the window closes.
                // Don't dispatch WM_CLOSE here — it triggers layout/paint cascades
                // that cause deadlocks with the render loop.
            };


            silkWindow.Render += (delta) =>
            {
                if (!renderReady)
                    return;

                // Flutter/wxWidgets pattern: the Render event is the ONLY place
                // where the GPU context is current.  We own the frame lifecycle here.
                int logicalW = silkWindow.Size.X;
                int logicalH = silkWindow.Size.Y;
                if (logicalW <= 0 || logicalH <= 0)
                    return;

                var (framebufferW, framebufferH) = ResolveFramebufferSize(silkWindow, logicalW, logicalH);
                if (framebufferW <= 0 || framebufferH <= 0)
                {
                    framebufferW = logicalW;
                    framebufferH = logicalH;
                }

                // Keep stored dimensions in sync
                if (_windows.TryGetValue(handle, out var ws))
                {
                    ws.Width = logicalW;
                    ws.Height = logicalH;
                    if (ws.LastLogicalWidth != logicalW || ws.LastLogicalHeight != logicalH
                        || ws.LastFramebufferWidth != framebufferW || ws.LastFramebufferHeight != framebufferH)
                    {
                        ws.LastLogicalWidth = logicalW;
                        ws.LastLogicalHeight = logicalH;
                        ws.LastFramebufferWidth = framebufferW;
                        ws.LastFramebufferHeight = framebufferH;
                        ws.Dirty = true;
                    }

                    long now = Environment.TickCount64;
                    if (now - ws.LastRenderTickMs < 33)
                    {
                        return;
                    }

                    ws.LastRenderTickMs = now;
                    if (!ws.Dirty)
                    {
                        return;
                    }
                }

                TracePaint($"[Render] hwnd=0x{(nint)handle:X} logical={logicalW}x{logicalH} framebuffer={framebufferW}x{framebufferH}");

                // Guard: catch managed exceptions during backend init
                System.Drawing.Graphics? g;
                try
                {
                    g = Drawing.Graphics.FromHwndInternal((IntPtr)handle);
                }
                catch (Exception ex)
                {
                    TracePaint($"[Render] Graphics.FromHwndInternal failed: {ex.GetType().Name}: {ex.Message}");
                    return; // Backend not ready yet
                }

                using (g)
                {
                    bool beganFrame = g.BeginFrame(framebufferW, framebufferH);
                    TracePaint($"[Render] BeginFrame={beganFrame} framebuffer={framebufferW}x{framebufferH}");
                    if (!beganFrame)
                        return;

                    try
                    {
                        // TODO: apply explicit logical->framebuffer scaling here once the
                        // transform pipeline is fully stabilized across message callbacks.

                        // Draw root form and children using the shared backend graphics.
                        // This stays on the PAL/Impeller path and avoids native GDI+ surfaces.
                        Control? root = ResolveTopLevelControl(handle);
                        TracePaint($"[Render] resolvedRoot={(root is null ? "null" : root.GetType().Name)} handle=0x{(nint)handle:X}");
                        if (root is null || root.Width <= 0 || root.Height <= 0)
                        {
                            TracePaint($"[Render] fallbackClear rootNullOrEmpty={root is null} rootSize={(root is null ? "n/a" : $"{root.Width}x{root.Height}")}");
                            g.Clear(System.Drawing.Color.FromArgb(34, 120, 255));
                        }
                        else
                        {
                            g.Clear(root.BackColor);
                            // Render in logical WinForms units and scale to framebuffer pixels.
                            // On Retina this keeps layout semantics while preserving sharp output.
                            var (sx, sy) = ResolveHiDpiScale(logicalW, logicalH, framebufferW, framebufferH);
                            TracePaint($"[HiDPI] mode={s_hiDpiMode} logical={logicalW}x{logicalH} framebuffer={framebufferW}x{framebufferH} scale={sx:0.###}x{sy:0.###}");
                            if (sx != 1f || sy != 1f)
                            {
                                g.ScaleTransform(sx, sy);
                            }

                            FixupTabPages(root);
                            var clip = new System.Drawing.Rectangle(0, 0, logicalW, logicalH);
                            PaintControlTree(root, g, clip, isRoot: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Render] Paint error: {ex}");
                    }

                    try
                    {
                        g.EndFrame(framebufferW, framebufferH);
                        if (_windows.TryGetValue(handle, out var rendered))
                        {
                            rendered.Dirty = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Render] EndFrame error: {ex}");
                    }
                }
            };

            silkWindow.Initialize();
            _windows[handle].SilkWindow = silkWindow;

            // Prime WinForms layout with an initial geometry sync. Silk.NET resize/move
            // callbacks are not guaranteed to fire on first show.
            var initialSize = silkWindow.Size;
            var initialPos = silkWindow.Position;
            PostMessageToControl(
                handle,
                handle,
                WM_MOVE,
                (WPARAM)0,
                (LPARAM)(nint)((initialPos.Y << 16) | (initialPos.X & 0xFFFF)));
            PostMessageToControl(
                handle,
                handle,
                WM_SIZE,
                (WPARAM)0,
                (LPARAM)(nint)((initialSize.Y << 16) | (initialSize.X & 0xFFFF)));
            MarkDirty(handle);

            // --- Wire up Silk.NET input to WinForms synthetic messages ---
            try
            {
                var input = silkWindow.CreateInput();
                _windows[handle].InputContext = input;

                foreach (var keyboard in input.Keyboards)
                {
                    keyboard.KeyDown += (kb, key, scancode) =>
                    {
                        MarkDirty(handle);
                        uint vk = SilkKeyToWin32(key);
                        PostMessageToControl(handle, handle, WM_KEYDOWN, (WPARAM)(nuint)vk, (LPARAM)(nint)scancode);
                    };
                    keyboard.KeyUp += (kb, key, scancode) =>
                    {
                        MarkDirty(handle);
                        uint vk = SilkKeyToWin32(key);
                        PostMessageToControl(handle, handle, WM_KEYUP, (WPARAM)(nuint)vk, (LPARAM)(nint)scancode);
                    };
                    keyboard.KeyChar += (kb, character) =>
                    {
                        MarkDirty(handle);
                        PostMessageToControl(handle, handle, WM_CHAR, (WPARAM)(nuint)character, (LPARAM)0);
                    };
                }

                foreach (var mouse in input.Mice)
                {
                    mouse.MouseMove += (m, position) =>
                    {
                        var pt = new System.Drawing.Point((int)position.X, (int)position.Y);
                        HWND target = HitTest(handle, pt, out var clientPt);
                        PostMessageToControl(target, handle, WM_MOUSEMOVE, (WPARAM)0, (LPARAM)(nint)(((int)clientPt.Y << 16) | ((int)clientPt.X & 0xFFFF)));
                    };
                    mouse.MouseDown += (m, button) =>
                    {
                        MarkDirty(handle);
                        uint msg = button switch
                        {
                            MouseButton.Left => WM_LBUTTONDOWN,
                            MouseButton.Right => WM_RBUTTONDOWN,
                            MouseButton.Middle => WM_MBUTTONDOWN,
                            _ => 0
                        };
                        if (msg != 0)
                        {
                            var pt = new System.Drawing.Point((int)m.Position.X, (int)m.Position.Y);
                            HWND target = HitTest(handle, pt, out var clientPt);

                            // Intercept TabControl click because we don't have native SysTabControl32 to process it
                            if (msg == WM_LBUTTONDOWN)
                            {
                                var ctrl = Control.FromHandle(target);
                                if (ctrl is TabControl tabControl)
                                {
                                    ctrl.BeginInvoke(new Action(() =>
                                    {
                                        for (int i = 0; i < tabControl.TabCount; i++)
                                        {
                                            if (tabControl.GetTabRect(i).Contains(clientPt))
                                            {
                                                tabControl.SelectedIndex = i;
                                                break;
                                            }
                                        }
                                    }));
                                }
                                else if (ctrl is ToolStrip toolStrip)
                                {
                                    ctrl.BeginInvoke(new Action(() =>
                                    {
                                        ToolStripItem? item = toolStrip.GetItemAt(clientPt);
                                        if (item is null || !item.Enabled)
                                        {
                                            return;
                                        }

                                        // Trigger standard ToolStrip behavior for menus/buttons.
                                        // This avoids relying on Win32 menu/message plumbing.
                                        if (item is ToolStripMenuItem menuItem)
                                        {
                                            menuItem.ShowDropDown();
                                        }
                                        else
                                        {
                                            item.PerformClick();
                                        }
                                    }));
                                }
                            }

                            PostMessageToControl(target, handle, msg, (WPARAM)0, (LPARAM)(nint)(((int)clientPt.Y << 16) | ((int)clientPt.X & 0xFFFF)));
                        }
                    };
                    mouse.MouseUp += (m, button) =>
                    {
                        MarkDirty(handle);
                        uint msg = button switch
                        {
                            MouseButton.Left => WM_LBUTTONUP,
                            MouseButton.Right => WM_RBUTTONUP,
                            MouseButton.Middle => WM_MBUTTONUP,
                            _ => 0
                        };
                        if (msg != 0)
                        {
                            var pt = new System.Drawing.Point((int)m.Position.X, (int)m.Position.Y);
                            HWND target = HitTest(handle, pt, out var clientPt);
                            PostMessageToControl(target, handle, msg, (WPARAM)0, (LPARAM)(nint)(((int)clientPt.Y << 16) | ((int)clientPt.X & 0xFFFF)));
                        }
                    };
                    mouse.Scroll += (m, scrollWheel) =>
                    {
                        MarkDirty(handle);
                        // Vertical scroll
                        if (scrollWheel.Y != 0)
                        {
                            var pt = new System.Drawing.Point((int)m.Position.X, (int)m.Position.Y);
                            HWND target = HitTest(handle, pt, out var clientPt);
                            PostMessageToControl(target, handle, WM_MOUSEWHEEL, (WPARAM)(nuint)((int)(scrollWheel.Y * 120) << 16), (LPARAM)(nint)(((int)clientPt.Y << 16) | ((int)clientPt.X & 0xFFFF)));
                        }

                        // Horizontal scroll
                        if (scrollWheel.X != 0)
                        {
                            var pt = new System.Drawing.Point((int)m.Position.X, (int)m.Position.Y);
                            HWND target = HitTest(handle, pt, out var clientPt);
                            PostMessageToControl(target, handle, WM_MOUSEHWHEEL, (WPARAM)(nuint)((int)(scrollWheel.X * 120) << 16), (LPARAM)(nint)(((int)clientPt.Y << 16) | ((int)clientPt.X & 0xFFFF)));
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Input] Failed to create input context: {ex.Message}");
            }

            // Store native HWND for Win32 message pumping
            try
            {
                if (silkWindow.Native?.Win32 is { } win32Native)
                {
                    _windows[handle].NativeHwnd = (HWND)(nint)win32Native.Hwnd;
                    Console.Error.WriteLine($"[Window] Native HWND = 0x{win32Native.Hwnd:X}");
                }
            }
            catch { }

            // Only allow rendering AFTER the window is fully initialized
            renderReady = true;
        }

        return handle;
    }

    /// <summary>
    /// Paint the entire control tree using the Impeller-backed Graphics.
    /// Instead of dispatching WM_PAINT (which creates a new Graphics per control via
    /// BeginPaint/HDC), we directly invoke OnPaint on each control using the shared
    /// Impeller Graphics that already has an active frame.
    /// This is the Impeller equivalent of the Win32 paint cycle.
    /// </summary>


    /// <summary>
    /// Recursively find TabControls and make their selected TabPage visible
    /// with correct bounds. In native WinForms, the Tab common control sends
    /// TCN_SELCHANGE which shows/sizes the selected page. We do it manually.
    /// </summary>
    private static void FixupTabPages(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is TabControl tc)
            {
                if (s_firstPaint)
                    Console.Error.WriteLine($"[FixupTabPages] Found TabControl. TabPages.Count={tc.TabPages.Count}");

                int selectedIndex = tc.SelectedIndex;
                if (selectedIndex < 0 && tc.TabPages.Count > 0)
                {
                    selectedIndex = 0; // Default to first tab if native state is uninitialized
                }

                if (selectedIndex >= 0 && selectedIndex < tc.TabPages.Count)
                {
                    var page = tc.TabPages[selectedIndex];
                    // Approximate the display rectangle (tab header area ~24px)
                    const int tabHeaderHeight = 24;
                    page.SetBounds(0, tabHeaderHeight, tc.Width, tc.Height - tabHeaderHeight);
                    page.Visible = true;
                    if (s_firstPaint)
                        Console.Error.WriteLine($"[FixupTabPages] Made '{page.Text}' visible: {page.Bounds}. Page Controls Count={page.Controls.Count}");
                    // Recurse into the now-visible page
                    FixupTabPages(page);
                }
            }
            else
            {
                FixupTabPages(c);
            }
        }
    }

    /// <summary>
    /// Recursively paint a control and all its children using the shared
    /// Impeller-backed Graphics object from the Render event handler.
    /// </summary>
    private static bool s_firstPaint = true;
    private static void PaintControlTree(Control control, System.Drawing.Graphics g, System.Drawing.Rectangle clipRect, bool isRoot = true)
    {
        try
        {
            TracePaint($"[Paint] {control.GetType().Name} visible={control.Visible} bounds={control.Bounds} children={control.Controls.Count}");
            if (s_firstPaint)
                Console.Error.WriteLine($"[Paint] {control.GetType().Name} @ ({control.Left},{control.Top}) {control.Width}x{control.Height} children={control.Controls.Count}");

            // PAL fallback paint: always draw control background via backend so controls
            // remain visible even when Win32/GDI-dependent paint paths throw on macOS.
            var bg = control.BackColor;
            if (bg.A > 0)
            {
                using var brush = new System.Drawing.SolidBrush(bg);
                int bgWidth = isRoot ? Math.Max(1, clipRect.Width) : Math.Max(1, control.Width);
                int bgHeight = isRoot ? Math.Max(1, clipRect.Height) : Math.Max(1, control.Height);
                g.FillRectangle(brush, 0, 0, bgWidth, bgHeight);
            }

            if (control is Label label && !string.IsNullOrEmpty(label.Text))
            {
                using var textBrush = new System.Drawing.SolidBrush(label.ForeColor);
                var textRect = new System.Drawing.RectangleF(0, 0, Math.Max(1, label.Width), Math.Max(1, label.Height));
                g.DrawString(label.Text, label.Font, textBrush, textRect);
            }
            else if (control is System.Windows.Forms.Button button)
            {
                DrawButton(button, g);
            }
            else if (control is MenuStrip menuStrip)
            {
                DrawMenuStrip(menuStrip, g);
            }
            else if (control is TabControl tabControl)
            {
                DrawTabHeaders(tabControl, g);
            }

            // Impeller-first clean-room mode: do not call Win32/GDI-dependent paint
            // dispatch for now. We recurse and paint primitive surfaces through backend.
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Paint ERROR] {control.GetType().Name}: {ex.Message}");
        }

        // Recurse into children (bottom-to-top Z-order)
        for (int i = control.Controls.Count - 1; i >= 0; i--)
        {
            var child = control.Controls[i];

            if (s_firstPaint)
                Console.Error.WriteLine($"  child[{i}] {child.GetType().Name} Visible={child.Visible} Bounds={child.Bounds}");

            if (!child.Visible || child.Width <= 0 || child.Height <= 0)
            {
                TracePaint($"[PaintChildSkip] {child.GetType().Name} visible={child.Visible} bounds={child.Bounds}");
                continue;
            }

            var state = g.Save();
            g.TranslateTransform(child.Left, child.Top);
            var childClip = new System.Drawing.Rectangle(0, 0, child.Width, child.Height);
            g.IntersectClip(childClip);

            PaintControlTree(child, g, childClip, isRoot: false);

            g.Restore(state);
        }

        if (isRoot)
            s_firstPaint = false;
    }

    private static void DrawButton(System.Windows.Forms.Button button, System.Drawing.Graphics g)
    {
        var face = button.BackColor.A == 0 ? System.Drawing.Color.FromArgb(74, 139, 255) : button.BackColor;
        using var fill = new System.Drawing.SolidBrush(face);
        g.FillRectangle(fill, 0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height));

        using var borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 40, 40));
        g.DrawRectangle(borderPen, 0, 0, Math.Max(1, button.Width) - 1, Math.Max(1, button.Height) - 1);

        if (!string.IsNullOrEmpty(button.Text))
        {
            using var textBrush = new System.Drawing.SolidBrush(button.ForeColor.A == 0 ? System.Drawing.Color.White : button.ForeColor);
            var textRect = new System.Drawing.RectangleF(0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height));
            using var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center,
                Trimming = System.Drawing.StringTrimming.EllipsisCharacter,
            };
            g.DrawString(button.Text, button.Font, textBrush, textRect, sf);
        }
    }

    private static void DrawMenuStrip(MenuStrip menuStrip, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(55, 55, 55));
        g.FillRectangle(bg, 0, 0, Math.Max(1, menuStrip.Width), Math.Max(1, menuStrip.Height));

        int x = 12;
        foreach (ToolStripItem item in menuStrip.Items)
        {
            string text = item.Text ?? string.Empty;
            if (text.Length == 0)
            {
                continue;
            }

            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 235, 235));
            g.DrawString(text, menuStrip.Font, brush, new System.Drawing.PointF(x, 4));
            x += 68;
        }
    }

    private static void DrawTabHeaders(TabControl tabControl, System.Drawing.Graphics g)
    {
        const int headerHeight = 30;
        int width = Math.Max(1, tabControl.Width);
        using var stripBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(230, 228, 212));
        g.FillRectangle(stripBg, 0, 0, width, headerHeight);

        int x = 8;
        int selected = tabControl.SelectedIndex;
        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            var tab = tabControl.TabPages[i];
            int tabWidth = 128;
            var rect = new System.Drawing.Rectangle(x, 2, tabWidth, headerHeight - 4);
            bool isSelected = i == selected;

            using var fill = new System.Drawing.SolidBrush(isSelected
                ? System.Drawing.Color.FromArgb(252, 252, 252)
                : System.Drawing.Color.FromArgb(210, 210, 196));
            g.FillRectangle(fill, rect);

            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 150, 140));
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);

            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(45, 45, 45));
            using var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center,
                Trimming = System.Drawing.StringTrimming.EllipsisCharacter,
            };
            g.DrawString(tab.Text, tabControl.Font, brush, rect, sf);
            x += tabWidth + 4;
            if (x >= width - 40)
            {
                break;
            }
        }
    }

    private static void TracePaint(string message)
    {
        if (string.IsNullOrWhiteSpace(s_traceFile))
        {
            return;
        }

        try
        {
            File.AppendAllText(s_traceFile, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static HiDpiMode ParseHiDpiMode()
    {
        string? raw = Environment.GetEnvironmentVariable("WINFORMSX_HIDPI_MODE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return HiDpiMode.Auto;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "0" or "off" or "false" => HiDpiMode.Off,
            "1" or "on" or "true" or "hidpi" => HiDpiMode.On,
            _ => HiDpiMode.Auto,
        };
    }

    private static (float ScaleX, float ScaleY) ResolveHiDpiScale(int logicalW, int logicalH, int framebufferW, int framebufferH)
    {
        if (s_hiDpiMode == HiDpiMode.Off)
        {
            return (1f, 1f);
        }

        float sx = logicalW > 0 ? framebufferW / (float)logicalW : 1f;
        float sy = logicalH > 0 ? framebufferH / (float)logicalH : 1f;

        if (s_hiDpiMode == HiDpiMode.On)
        {
            return (sx <= 0f ? 1f : sx, sy <= 0f ? 1f : sy);
        }

        // Auto: only scale when framebuffer diverges from logical size.
        if (Math.Abs(sx - 1f) < 0.01f && Math.Abs(sy - 1f) < 0.01f)
        {
            return (1f, 1f);
        }

        return (sx <= 0f ? 1f : sx, sy <= 0f ? 1f : sy);
    }

    private static float ParseHiDpiScaleOverride()
    {
        string? raw = Environment.GetEnvironmentVariable("WINFORMSX_HIDPI_SCALE");
        if (!float.TryParse(raw, out float parsed) || parsed <= 0f)
        {
            return 1f;
        }

        return parsed;
    }

    private void MarkDirty(HWND hWnd)
    {
        if (_windows.TryGetValue(hWnd, out var state))
        {
            state.Dirty = true;
        }
    }

    private static (int Width, int Height) ResolveFramebufferSize(IWindow window, int fallbackWidth, int fallbackHeight)
    {
        int width = fallbackWidth;
        int height = fallbackHeight;

        try
        {
            var property = window.GetType().GetProperty("FramebufferSize");
            if (property?.GetValue(window) is object value)
            {
                var xProp = value.GetType().GetProperty("X");
                var yProp = value.GetType().GetProperty("Y");
                if (xProp?.GetValue(value) is int w && yProp?.GetValue(value) is int h && w > 0 && h > 0)
                {
                    width = w;
                    height = h;
                }
            }
        }
        catch
        {
        }

        // Some macOS/Vulkan runtime paths report framebuffer == logical even on Retina.
        // Allow explicit cross-platform override so HiDPI mode is testable/deterministic.
        if (s_hiDpiMode == HiDpiMode.On && s_hiDpiScaleOverride > 1f
            && width == fallbackWidth && height == fallbackHeight)
        {
            width = Math.Max(1, (int)Math.Round(fallbackWidth * s_hiDpiScaleOverride));
            height = Math.Max(1, (int)Math.Round(fallbackHeight * s_hiDpiScaleOverride));
        }

        return (width, height);
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
                WM_SHOWWINDOW,
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
                if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOZORDER))
                {
                    if (insertAfter == (HWND)(-1)) // HWND_TOPMOST
                        s.SilkWindow.TopMost = true;
                    else if (insertAfter == (HWND)(-2)) // HWND_NOTOPMOST
                        s.SilkWindow.TopMost = false;
                }
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
                hWnd, WM_WINDOWPOSCHANGED, (WPARAM)0, lParam, out _);

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

    public nint SetClassLong(HWND hWnd, GET_CLASS_LONG_INDEX index, nint value)
    {
        // Impeller backend has no Win32 class proc/table; treat as a successful no-op.
        return value;
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

    private static uint SilkKeyToWin32(Key key)
    {
        return key switch
        {
            Key.Backspace => 0x08, // VK_BACK
            Key.Tab => 0x09, // VK_TAB
            Key.Enter => 0x0D, // VK_RETURN
            Key.ShiftLeft => 0xA0, // VK_LSHIFT
            Key.ShiftRight => 0xA1, // VK_RSHIFT
            Key.ControlLeft => 0xA2, // VK_LCONTROL
            Key.ControlRight => 0xA3, // VK_RCONTROL
            Key.AltLeft => 0xA4, // VK_LMENU
            Key.AltRight => 0xA5, // VK_RMENU
            Key.Pause => 0x13, // VK_PAUSE
            Key.CapsLock => 0x14, // VK_CAPITAL
            Key.Escape => 0x1B, // VK_ESCAPE
            Key.Space => 0x20, // VK_SPACE
            Key.PageUp => 0x21, // VK_PRIOR
            Key.PageDown => 0x22, // VK_NEXT
            Key.End => 0x23, // VK_END
            Key.Home => 0x24, // VK_HOME
            Key.Left => 0x25, // VK_LEFT
            Key.Up => 0x26, // VK_UP
            Key.Right => 0x27, // VK_RIGHT
            Key.Down => 0x28, // VK_DOWN
            Key.PrintScreen => 0x2C, // VK_SNAPSHOT
            Key.Insert => 0x2D, // VK_INSERT
            Key.Delete => 0x2E, // VK_DELETE
            Key.Number0 => 0x30,
            Key.Number1 => 0x31,
            Key.Number2 => 0x32,
            Key.Number3 => 0x33,
            Key.Number4 => 0x34,
            Key.Number5 => 0x35,
            Key.Number6 => 0x36,
            Key.Number7 => 0x37,
            Key.Number8 => 0x38,
            Key.Number9 => 0x39,
            Key.A => 0x41,
            Key.B => 0x42,
            Key.C => 0x43,
            Key.D => 0x44,
            Key.E => 0x45,
            Key.F => 0x46,
            Key.G => 0x47,
            Key.H => 0x48,
            Key.I => 0x49,
            Key.J => 0x4A,
            Key.K => 0x4B,
            Key.L => 0x4C,
            Key.M => 0x4D,
            Key.N => 0x4E,
            Key.O => 0x4F,
            Key.P => 0x50,
            Key.Q => 0x51,
            Key.R => 0x52,
            Key.S => 0x53,
            Key.T => 0x54,
            Key.U => 0x55,
            Key.V => 0x56,
            Key.W => 0x57,
            Key.X => 0x58,
            Key.Y => 0x59,
            Key.Z => 0x5A,
            Key.F1 => 0x70,
            Key.F2 => 0x71,
            Key.F3 => 0x72,
            Key.F4 => 0x73,
            Key.F5 => 0x74,
            Key.F6 => 0x75,
            Key.F7 => 0x76,
            Key.F8 => 0x77,
            Key.F9 => 0x78,
            Key.F10 => 0x79,
            Key.F11 => 0x7A,
            Key.F12 => 0x7B,
            Key.Keypad0 => 0x60,
            Key.Keypad1 => 0x61,
            Key.Keypad2 => 0x62,
            Key.Keypad3 => 0x63,
            Key.Keypad4 => 0x64,
            Key.Keypad5 => 0x65,
            Key.Keypad6 => 0x66,
            Key.Keypad7 => 0x67,
            Key.Keypad8 => 0x68,
            Key.Keypad9 => 0x69,
            Key.KeypadMultiply => 0x6A,
            Key.KeypadAdd => 0x6B,
            Key.KeypadSubtract => 0x6D,
            Key.KeypadDecimal => 0x6E,
            Key.KeypadDivide => 0x6F,
            _ => (uint)key
        };
    }

    private HWND HitTest(HWND root, System.Drawing.Point pt, out System.Drawing.Point clientPt)
    {
        clientPt = pt;
        HWND current = root;

        while (true)
        {
            HWND foundChild = HWND.Null;
            var ctrl = Control.FromHandle(current);
            if (ctrl is object)
            {
                // In WinForms, Controls[0] is at the top of the Z-order
                for (int i = 0; i < ctrl.Controls.Count; i++)
                {
                    var child = ctrl.Controls[i];
                    if (child.Visible &&
                        clientPt.X >= child.Left && clientPt.X < child.Right &&
                        clientPt.Y >= child.Top && clientPt.Y < child.Bottom)
                    {
                        foundChild = (HWND)(nint)child.Handle;
                        clientPt.X -= child.Left;
                        clientPt.Y -= child.Top;
                        break;
                    }
                }
            }

            if (foundChild == HWND.Null)
                break;

            current = foundChild;
        }

        return current;
    }

    private void PostMessageToControl(HWND targetWindow, HWND fallbackTopLevel, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var ctrl = Control.FromHandle(targetWindow) ?? Control.FromHandle(fallbackTopLevel) ?? ResolveTopLevelControl(fallbackTopLevel);

        if (ctrl is object)
        {
            HWND dispatchTarget = Control.FromHandle(targetWindow) is null
                ? (HWND)(nint)ctrl.Handle
                : targetWindow;
            ctrl.BeginInvoke(new Action(() =>
            {
                NativeWindow.DispatchMessageDirect(dispatchTarget, msg, wParam, lParam, out _);
            }));
        }
    }

    private static Control? ResolveTopLevelControl(HWND preferredHandle)
    {
        Control? resolved = Control.FromHandle(preferredHandle);
        if (resolved is not null)
        {
            return resolved;
        }

        foreach (Form form in Application.OpenForms)
        {
            if (form.Visible)
            {
                return form;
            }
        }

        return Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
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
    public IInputContext? InputContext;
    public HWND NativeHwnd;
    public long LastRenderTickMs;
    public bool Dirty = true;
    public int LastLogicalWidth;
    public int LastLogicalHeight;
    public int LastFramebufferWidth;
    public int LastFramebufferHeight;
}

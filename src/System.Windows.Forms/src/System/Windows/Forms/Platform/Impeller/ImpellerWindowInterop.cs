// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

using System.Runtime.InteropServices;
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
    private const nuint MK_LBUTTON = 0x0001;
    private const nuint MK_RBUTTON = 0x0002;
    private const nuint MK_MBUTTON = 0x0010;
    private const nuint MkAnyButtonMask = MK_LBUTTON | MK_RBUTTON | MK_MBUTTON;
    private const int WheelDelta = 120;
    private const int DefaultTargetFrameRate = 60;
    private const int ManagedTabHeaderHeight = 30;
    private const int ManagedTabHeaderLeft = 6;
    private const int ManagedTabHeaderWidth = 128;
    private const int ManagedTabHeaderGap = 0;
    private const int ManagedMenuItemLeft = 3;
    private const int ManagedMenuItemMinWidth = 34;
    private const int ManagedMenuItemHorizontalPadding = 8;
    private const int ManagedMenuItemGap = 1;
    private const int ManagedMenuItemHeight = 22;
    private const int ManagedMenuDropDownMinWidth = 158;
    private const int ManagedMenuDropDownRowHeight = 22;
    private const int ManagedTextPadding = 4;
    private const int ManagedCheckGlyphSize = 14;
    private const int ManagedListItemHeight = 22;
    private const int ManagedComboItemHeight = 24;
    private const int ManagedComboDropDownMaxVisibleItems = 8;
    private const int ManagedTreeItemHeight = 22;
    private const int ManagedListViewHeaderHeight = 24;
    private const int ManagedDataGridHeaderHeight = 24;
    private const int ManagedDataGridRowHeight = 24;
    private const int ManagedScrollBarArrowLength = 17;
    private const int ManagedScrollBarMinThumbLength = 18;
    private const uint CwpSkipInvisible = 0x0001;
    private const uint CwpSkipDisabled = 0x0002;
    private const uint CwpSkipTransparent = 0x0004;
    private const int CwUseDefault = unchecked((int)0x80000000);
    private const int DefaultTopLevelWindowX = 100;
    private const int DefaultTopLevelWindowY = 100;
    private const int DefaultDirtyCoalesceMs = 33;
    private const int DefaultRenderFailureCooldownMs = 250;
    private static readonly System.Drawing.Color s_managedControlColor = System.Drawing.Color.FromArgb(240, 240, 240);
    private static readonly System.Drawing.Color s_managedControlLightColor = System.Drawing.Color.FromArgb(227, 227, 227);
    private static readonly System.Drawing.Color s_managedControlDarkColor = System.Drawing.Color.FromArgb(160, 160, 160);
    private static readonly System.Drawing.Color s_managedControlDarkDarkColor = System.Drawing.Color.FromArgb(105, 105, 105);
    private static readonly System.Drawing.Color s_managedMenuColor = System.Drawing.Color.FromArgb(249, 249, 249);

    // Internal window registry: HWND -> ImpellerSurface mapping
    private static long s_nextHandle = 0x10000;
    private static readonly string? s_traceFile = Environment.GetEnvironmentVariable("WINFORMSX_TRACE_FILE");
    private static readonly HiDpiMode s_hiDpiMode = ParseHiDpiMode();
    private static readonly float s_hiDpiScaleOverride = ParseHiDpiScaleOverride();
    private static readonly long s_targetFrameIntervalMs = ParseTargetFrameIntervalMs();
    private static readonly long s_dirtyCoalesceMs = ParseMilliseconds("WINFORMSX_DIRTY_COALESCE_MS", DefaultDirtyCoalesceMs, 0, 250);
    private static readonly long s_renderFailureCooldownMs = ParseMilliseconds("WINFORMSX_RENDER_FAILURE_COOLDOWN_MS", DefaultRenderFailureCooldownMs, 0, 2000);
    private static readonly Dictionary<ListView, int> s_managedListViewSelection = [];
    private static readonly Dictionary<ListView, int> s_managedListViewTopRow = [];
    private static readonly Dictionary<TreeView, int> s_managedTreeViewTopRow = [];
    private static readonly Dictionary<DataGridView, int> s_managedDataGridViewSelection = [];
    private static readonly Dictionary<DataGridView, int> s_managedDataGridViewTopRow = [];
    private static readonly Dictionary<ToolStrip, ToolStripItem> s_managedActiveMenuItems = [];
    private static readonly HashSet<Control> s_pressedControls = [];
    private static readonly object s_pendingMouseMoveLock = new();
    private static readonly HashSet<(nint Handle, nuint KeyState)> s_pendingMouseMoveTargets = [];
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
        foreach (var state in _windows.Values.ToArray())
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
        bool isTopLevel = hWndParent == HWND.Null;
        int effectiveX = isTopLevel ? NormalizeTopLevelWindowCoordinate(x, DefaultTopLevelWindowX) : NormalizeChildWindowCoordinate(x);
        int effectiveY = isTopLevel ? NormalizeTopLevelWindowCoordinate(y, DefaultTopLevelWindowY) : NormalizeChildWindowCoordinate(y);

        // Some create paths hand us title-bar-sized bounds (e.g., 136x39) for top-level
        // forms. Clamp to a sane minimum so the render surface is usable.
        if (isTopLevel)
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
            X = effectiveX,
            Y = effectiveY,
            Width = effectiveWidth,
            Height = effectiveHeight,
            Parent = hWndParent,
            Visible = false,
            // Sentinel default WndProc — the framework expects a non-null prior
            // WndProc when subclassing via AssignHandle/SetWindowLong.
            WndProc = 0x1,
        };

        if (isTopLevel && ShouldCreateBackendForHiddenTopLevelWindow())
        {
            var options = WindowOptions.DefaultVulkan;
            options.Title = lpWindowName ?? string.Empty;
            options.Position = new Vector2D<int>(effectiveX, effectiveY);
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

            var silkWindow = CreateInitializedSilkWindow(options);
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
                        ws.DeferRenderUntilTickMs = 0;
                    }

                    long now = Environment.TickCount64;
                    if (ws.HasPresentedFrame && now - ws.LastRenderTickMs < s_targetFrameIntervalMs)
                    {
                        return;
                    }

                    if (ws.Dirty && ws.HasPresentedFrame && ws.DeferRenderUntilTickMs > now)
                    {
                        return;
                    }

                    if (ws.DeferRenderUntilTickMs <= now)
                    {
                        ws.DeferRenderUntilTickMs = 0;
                    }

                    if (!ws.Dirty)
                    {
                        return;
                    }

                    ws.LastRenderTickMs = now;
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

                    bool paintSucceeded = false;
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
                            using var paintGuard = WinFormsXExecutionGuard.Enter(
                                WinFormsXExecutionKind.Paint,
                                $"Render hwnd=0x{(nint)handle:X} root={root.GetType().Name}");

                            SynchronizeRootControlSize(root, logicalW, logicalH);
                            g.Clear(NormalizeSystemColor(root.BackColor));
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
                            if (_windows.TryGetValue(handle, out var menuState))
                            {
                                g.FlushPending();
                                DrawManagedMenuDropDown(menuState, g);
                                DrawManagedComboDropDown(menuState, g);
                            }
                        }

                        paintSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Render] Paint error: {ex}");
                    }

                    if (!paintSucceeded)
                    {
                        g.AbortFrame();
                        if (_windows.TryGetValue(handle, out var failed))
                        {
                            failed.Dirty = true;
                        }

                        return;
                    }

                    try
                    {
                        g.EndFrame(framebufferW, framebufferH);
                        if (_windows.TryGetValue(handle, out var rendered))
                        {
                            rendered.Dirty = false;
                            rendered.HasPresentedFrame = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Render] EndFrame error: {ex}");
                        if (_windows.TryGetValue(handle, out var failed))
                        {
                            failed.Dirty = true;
                            failed.DeferRenderUntilTickMs = Environment.TickCount64 + s_renderFailureCooldownMs;
                        }
                    }
                }
            };

            _windows[handle].SilkWindow = silkWindow;

            // Prime WinForms layout with an initial geometry sync. Silk.NET resize/move
            // callbacks are not guaranteed to fire on first show.
            PostMessageToControl(
                handle,
                handle,
                WM_MOVE,
                (WPARAM)0,
                (LPARAM)(nint)((effectiveY << 16) | (effectiveX & 0xFFFF)));
            PostMessageToControl(
                handle,
                handle,
                WM_SIZE,
                (WPARAM)0,
                (LPARAM)(nint)((effectiveHeight << 16) | (effectiveWidth & 0xFFFF)));
            MarkDirtyImmediate(handle);

            // --- Wire up Silk.NET input to WinForms synthetic messages ---
            try
            {
                var input = silkWindow.CreateInput();
                _windows[handle].InputContext = input;

                foreach (var keyboard in input.Keyboards)
                {
                    keyboard.KeyDown += (kb, key, scancode) =>
                    {
                        HWND target = PlatformApi.Input.GetFocus();
                        if (target == HWND.Null)
                        {
                            target = handle;
                        }

                        if (TryHandleManagedTextBoxKeyDown(handle, target, kb, key))
                        {
                            return;
                        }

                        if (TryHandleManagedControlKeyDown(handle, target, kb, key))
                        {
                            return;
                        }

                        uint vk = SilkKeyToWin32(key);
                        PostMessageToControl(target, handle, WM_KEYDOWN, (WPARAM)(nuint)vk, (LPARAM)(nint)scancode);
                        MarkDirty(handle);
                    };
                    keyboard.KeyUp += (kb, key, scancode) =>
                    {
                        HWND target = PlatformApi.Input.GetFocus();
                        if (target == HWND.Null)
                        {
                            target = handle;
                        }

                        uint vk = SilkKeyToWin32(key);
                        PostMessageToControl(target, handle, WM_KEYUP, (WPARAM)(nuint)vk, (LPARAM)(nint)scancode);
                        MarkDirty(handle);
                    };
                    keyboard.KeyChar += (kb, character) =>
                    {
                        HWND target = PlatformApi.Input.GetFocus();
                        if (target == HWND.Null)
                        {
                            target = handle;
                        }

                        if (TryHandleManagedTextBoxKeyChar(handle, target, kb, character))
                        {
                            return;
                        }

                        PostMessageToControl(target, handle, WM_CHAR, (WPARAM)(nuint)character, (LPARAM)0);
                        MarkDirty(handle);
                    };
                }

                foreach (var mouse in input.Mice)
                {
                    mouse.MouseMove += (m, position) =>
                    {
                        using var guard = WinFormsXExecutionGuard.Enter(WinFormsXExecutionKind.Input, "MouseMove");
                        var pt = GetLogicalMousePoint(handle, m);
                        SetCursorPosFromRootClient(handle, pt);
                        HWND target = PlatformApi.Input.GetCapture();
                        System.Drawing.Point clientPt;
                        if (target == HWND.Null || !TryGetClientPoint(target, pt, out clientPt))
                        {
                            target = HitTestRootClient(handle, pt, out clientPt);
                        }

                        if (TryUpdateManagedTextBoxSelectionDrag(handle, target, clientPt))
                        {
                            return;
                        }

                        PostMessageToControl(target, handle, WM_MOUSEMOVE, (WPARAM)0, PackMouseLParam(clientPt));
                    };
                    mouse.MouseDown += (m, button) =>
                    {
                        using var guard = WinFormsXExecutionGuard.Enter(WinFormsXExecutionKind.Input, $"MouseDown {button}");
                        uint msg = button switch
                        {
                            MouseButton.Left => WM_LBUTTONDOWN,
                            MouseButton.Right => WM_RBUTTONDOWN,
                            MouseButton.Middle => WM_MBUTTONDOWN,
                            _ => 0
                        };
                        if (msg != 0)
                        {
                            var pt = GetLogicalMousePoint(handle, m);
                            if (msg == WM_LBUTTONDOWN && TryHandleManagedComboOverlayClick(handle, pt))
                            {
                                return;
                            }

                            if (msg == WM_LBUTTONDOWN && TryHandleManagedMenuOverlayClick(handle, pt))
                            {
                                return;
                            }

                            SetCursorPosFromRootClient(handle, pt);
                            HWND target = HitTestRootClient(handle, pt, out var clientPt);
                            Control? ctrl = Control.FromHandle(target);
                            TraceInput($"[MouseDown] button={button} pt={pt} target=0x{(nint)target:X} control={ctrl?.GetType().Name ?? "<none>"} client={clientPt}");
                            if (msg == WM_LBUTTONDOWN && _windows.TryGetValue(handle, out var mouseDownState))
                            {
                                mouseDownState.MouseDownTarget = target;
                                mouseDownState.PressedControl = ctrl;
                                if (mouseDownState.PressedControl is not null)
                                {
                                    s_pressedControls.Add(mouseDownState.PressedControl);
                                }
                            }

                            PlatformApi.Input.SetCapture(target);
                            PlatformApi.Input.SetFocus(target);
                            PlatformApi.Input.SetActiveWindow(handle);
                            bool managedMouseDown = msg == WM_LBUTTONDOWN
                                && (ctrl is TabControl
                                    || ctrl is ToolStrip
                                    || ctrl is TextBoxBase
                                    || IsManagedClickControl(ctrl));
                            bool paintsPressedState = IsManagedPushButton(ctrl);
                            if (paintsPressedState)
                            {
                                MarkDirtyImmediate(handle);
                            }
                            else if (!managedMouseDown)
                            {
                                MarkDirty(handle);
                            }

                            // Intercept TabControl click because we don't have native SysTabControl32 to process it
                            if (msg == WM_LBUTTONDOWN)
                            {
                                if (ctrl is TabControl tabControl)
                                {
                                    ctrl.BeginInvoke(new Action(() =>
                                    {
                                        int tabIndex = GetManagedTabIndexAtPoint(tabControl, clientPt);
                                        if (tabIndex >= 0)
                                        {
                                            TraceInput($"[TabClick] index={tabIndex} text={tabControl.TabPages[tabIndex].Text}");
                                            SelectTabSafe(tabControl, tabIndex);
                                            MarkDirty(handle);
                                        }
                                    }));
                                }
                                else if (ctrl is ToolStrip toolStrip)
                                {
                                    ctrl.BeginInvoke(new Action(() =>
                                    {
                                        ToolStripItem? item = GetManagedMenuItemAtPoint(toolStrip, clientPt) ?? toolStrip.GetItemAt(clientPt);
                                        TraceInput($"[ToolStripClick] point={clientPt} item={item?.GetType().Name ?? "<none>"} text={item?.Text ?? string.Empty}");
                                        if (item is null || !item.Enabled)
                                        {
                                            return;
                                        }

                                        if (item is ToolStripMenuItem menuItem)
                                        {
                                            if (menuItem.HasDropDownItems)
                                            {
                                                ShowManagedMenuDropDown(handle, toolStrip, menuItem, clientPt);
                                            }
                                            else
                                            {
                                                PerformToolStripItemClick(handle, menuItem);
                                            }
                                        }
                                        else
                                        {
                                            PerformToolStripItemClick(handle, item);
                                        }
                                    }));

                                    return;
                                }
                                else if (ctrl is TextBoxBase textBox)
                                {
                                    BeginManagedTextBoxMouseDown(handle, textBox, clientPt);
                                    return;
                                }
                                else if (IsManagedClickControl(ctrl))
                                {
                                    return;
                                }
                            }

                            PostMessageToControl(target, handle, msg, (WPARAM)0, PackMouseLParam(clientPt));
                        }
                    };
                    mouse.MouseUp += (m, button) =>
                    {
                        using var guard = WinFormsXExecutionGuard.Enter(WinFormsXExecutionKind.Input, $"MouseUp {button}");
                        uint msg = button switch
                        {
                            MouseButton.Left => WM_LBUTTONUP,
                            MouseButton.Right => WM_RBUTTONUP,
                            MouseButton.Middle => WM_MBUTTONUP,
                            _ => 0
                        };
                        if (msg != 0)
                        {
                            var pt = GetLogicalMousePoint(handle, m);
                            HWND target = PlatformApi.Input.GetCapture();
                            System.Drawing.Point clientPt;
                            if (target == HWND.Null || !TryGetClientPoint(target, pt, out clientPt))
                            {
                                target = HitTestRootClient(handle, pt, out clientPt);
                            }

                            if (msg == WM_LBUTTONUP && _windows.TryGetValue(handle, out var mouseUpState))
                            {
                                Control? releasedPressedControl = mouseUpState.PressedControl;
                                if (releasedPressedControl is not null)
                                {
                                    s_pressedControls.Remove(releasedPressedControl);
                                    mouseUpState.PressedControl = null;
                                }

                                bool clickMatchesMouseDown = mouseUpState.MouseDownTarget == target;
                                mouseUpState.MouseDownTarget = HWND.Null;
                                mouseUpState.ActiveTextSelectionBox = null;
                                PlatformApi.Input.ReleaseCapture();
                                bool handledManagedClick = clickMatchesMouseDown
                                    && TryPerformManagedControlClick(handle, target, clientPt);
                                if (IsManagedPushButton(releasedPressedControl))
                                {
                                    MarkDirtyImmediate(handle);
                                }
                                else if (!handledManagedClick)
                                {
                                    MarkDirty(handle);
                                }

                                if (handledManagedClick)
                                {
                                    return;
                                }
                            }

                            if (Control.FromHandle(target) is ToolStrip)
                            {
                                return;
                            }

                            PostMessageToControl(target, handle, msg, (WPARAM)0, PackMouseLParam(clientPt));
                        }
                    };
                    mouse.Scroll += (m, scrollWheel) =>
                    {
                        using var guard = WinFormsXExecutionGuard.Enter(WinFormsXExecutionKind.Input, "MouseScroll");
                        int wheelDelta = GetMouseWheelDelta(scrollWheel);
                        if (wheelDelta == 0)
                        {
                            return;
                        }

                        var pt = GetLogicalMousePoint(handle, m);
                        SetCursorPosFromRootClient(handle, pt);
                        HWND target = HitTestRootClient(handle, pt, out var clientPt);
                        TraceInput(
                            $"[MouseScroll] pt={pt} target=0x{(nint)target:X} delta={wheelDelta} client={clientPt}");
                        if (!TryHandleManagedMouseWheel(handle, target, wheelDelta))
                        {
                            TraceInput($"[MouseScroll] no managed wheel handler for target=0x{(nint)target:X}");
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

    private static bool ShouldCreateBackendForHiddenTopLevelWindow()
        => Environment.GetEnvironmentVariable("WINFORMSX_SUPPRESS_HIDDEN_BACKEND") != "1";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlfwInitVulkanLoaderDelegate(nint loader);

    private static GlfwInitVulkanLoaderDelegate? s_glfwInitVulkanLoader;

    private static IWindow CreateInitializedSilkWindow(WindowOptions vulkanOptions)
    {
        if (VulkanLoaderResolver.TryConfigureRuntime(out string? runtimeDetail))
        {
            TracePaint($"[Window] Vulkan runtime configured: {runtimeDetail}");
        }
        else
        {
            TracePaint($"[Window] Vulkan runtime auto-config unavailable: {runtimeDetail ?? "<no detail>"}");
        }

        if (VulkanLoaderResolver.TryEnsureLoaded(out string? loadedFrom))
        {
            TracePaint($"[Window] Vulkan loader preloaded from {loadedFrom}");
        }
        else
        {
            TracePaint("[Window] Vulkan loader not found in default/system paths; GLFW Vulkan support may be unavailable.");
        }

        if (VulkanLoaderResolver.TryGetExport("vkGetInstanceProcAddr", out nint vkGetInstanceProcAddr, out string? vkProcSource)
            && vkGetInstanceProcAddr != nint.Zero)
        {
            if (TryInitializeGlfwVulkanLoader(vkGetInstanceProcAddr, out string? glfwDetail))
            {
                TracePaint($"[Window] GLFW Vulkan loader initialized from {vkProcSource ?? "<unknown>"} via {glfwDetail}");
            }
            else
            {
                TracePaint($"[Window] GLFW Vulkan loader initialization unavailable: {glfwDetail ?? "<no detail>"}");
            }
        }

        try
        {
            IWindow vulkanWindow = Window.Create(vulkanOptions);
            vulkanWindow.Initialize();
            return vulkanWindow;
        }
        catch (Exception ex) when (IsVulkanWindowUnavailable(ex))
        {
            TracePaint($"[Window] Vulkan window unavailable; falling back to default Silk window. {ex.GetType().Name}: {ex.Message}");

            WindowOptions fallbackOptions = WindowOptions.Default;
            fallbackOptions.Title = vulkanOptions.Title;
            fallbackOptions.Position = vulkanOptions.Position;
            fallbackOptions.Size = vulkanOptions.Size;
            fallbackOptions.IsVisible = vulkanOptions.IsVisible;
            fallbackOptions.WindowBorder = vulkanOptions.WindowBorder;
            fallbackOptions.TopMost = vulkanOptions.TopMost;
            fallbackOptions.FramesPerSecond = vulkanOptions.FramesPerSecond;
            fallbackOptions.UpdatesPerSecond = vulkanOptions.UpdatesPerSecond;
            fallbackOptions.ShouldSwapAutomatically = false;

            IWindow fallbackWindow = Window.Create(fallbackOptions);
            fallbackWindow.Initialize();
            return fallbackWindow;
        }
    }

    private static bool TryInitializeGlfwVulkanLoader(nint vkGetInstanceProcAddr, out string? detail)
    {
        if (s_glfwInitVulkanLoader is not null)
        {
            s_glfwInitVulkanLoader(vkGetInstanceProcAddr);
            detail = "cached glfwInitVulkanLoader";
            return true;
        }

        foreach (string candidate in GetGlfwCandidates())
        {
            if (!NativeLibrary.TryLoad(candidate, out nint glfwHandle) || glfwHandle == nint.Zero)
            {
                continue;
            }

            if (!NativeLibrary.TryGetExport(glfwHandle, "glfwInitVulkanLoader", out nint export)
                || export == nint.Zero)
            {
                continue;
            }

            s_glfwInitVulkanLoader = Marshal.GetDelegateForFunctionPointer<GlfwInitVulkanLoaderDelegate>(export);
            s_glfwInitVulkanLoader(vkGetInstanceProcAddr);
            detail = candidate;
            return true;
        }

        detail = "glfwInitVulkanLoader export not found";
        return false;
    }

    private static IEnumerable<string> GetGlfwCandidates()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] relativeCandidates =
        [
            "libglfw.3.dylib",
            "runtimes/osx-arm64/native/libglfw.3.dylib",
            "runtimes/osx-x64/native/libglfw.3.dylib",
            "runtimes/linux-arm64/native/libglfw.so.3",
            "runtimes/linux-x64/native/libglfw.so.3",
        ];

        foreach (string relative in relativeCandidates)
        {
            yield return Path.Combine(baseDirectory, relative);
        }

        yield return "libglfw.3.dylib";
        yield return "libglfw.so.3";
        yield return "glfw3.dll";
    }

    private static bool IsVulkanWindowUnavailable(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            string message = current.Message;
            if (message.Contains("Vulkan", StringComparison.OrdinalIgnoreCase)
                || message.Contains("GLFW", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int NormalizeTopLevelWindowCoordinate(int value, int fallback)
        => value is CwUseDefault or < -32000 or > 32000 ? fallback : value;

    private static int NormalizeChildWindowCoordinate(int value)
        => value is CwUseDefault or < -32000 or > 32000 ? 0 : value;

    /// <summary>
    /// Keep root bounds synced with the Silk window so Dock/Anchor layouts expand before painting.
    /// </summary>
    private static void SynchronizeRootControlSize(Control root, int logicalWidth, int logicalHeight)
    {
        if (logicalWidth <= 0 || logicalHeight <= 0)
        {
            return;
        }

        if (root.Width == logicalWidth && root.Height == logicalHeight)
        {
            return;
        }

        TracePaint($"[RootResize] {root.GetType().Name} {root.Width}x{root.Height} -> {logicalWidth}x{logicalHeight}");
        root.SetBounds(root.Left, root.Top, logicalWidth, logicalHeight, BoundsSpecified.Size);
        root.PerformLayout();
    }

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
                    var selectedBounds = new System.Drawing.Rectangle(
                        0,
                        ManagedTabHeaderHeight,
                        tc.Width,
                        Math.Max(1, tc.Height - ManagedTabHeaderHeight));

                    for (int i = 0; i < tc.TabPages.Count; i++)
                    {
                        var page = tc.TabPages[i];
                        bool shouldBeVisible = i == selectedIndex;
                        if (page.Visible != shouldBeVisible)
                        {
                            page.Visible = shouldBeVisible;
                        }

                        if (shouldBeVisible && page.Bounds != selectedBounds)
                        {
                            page.SetBounds(
                                selectedBounds.X,
                                selectedBounds.Y,
                                selectedBounds.Width,
                                selectedBounds.Height);
                        }

                        if (shouldBeVisible)
                        {
                            if (s_firstPaint)
                                Console.Error.WriteLine($"[FixupTabPages] Made '{page.Text}' visible: {page.Bounds}. Page Controls Count={page.Controls.Count}");

                            // Recurse into the now-visible page.
                            FixupTabPages(page);
                        }
                    }
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
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.Paint,
            $"PaintControlTree {control.GetType().Name} bounds={control.Bounds}");

        try
        {
            TracePaint($"[Paint] {control.GetType().Name} visible={control.Visible} bounds={control.Bounds} children={control.Controls.Count}");
            if (s_firstPaint)
                Console.Error.WriteLine($"[Paint] {control.GetType().Name} @ ({control.Left},{control.Top}) {control.Width}x{control.Height} children={control.Controls.Count}");

            // PAL fallback paint: always draw control background via backend so controls
            // remain visible even when Win32/GDI-dependent paint paths throw on macOS.
            var bg = NormalizeSystemColor(control.BackColor);
            if (bg.A > 0)
            {
                using var brush = new System.Drawing.SolidBrush(bg);
                int bgWidth = isRoot ? Math.Max(1, clipRect.Width) : Math.Max(1, control.Width);
                int bgHeight = isRoot ? Math.Max(1, clipRect.Height) : Math.Max(1, control.Height);
                g.FillRectangle(brush, 0, 0, bgWidth, bgHeight);
            }

            bool handled = true;
            if (control is Label label && !string.IsNullOrEmpty(label.Text))
            {
                using var textBrush = new System.Drawing.SolidBrush(label.ForeColor);
                var textRect = new System.Drawing.RectangleF(0, 0, Math.Max(1, label.Width), Math.Max(1, label.Height));
                g.DrawString(SanitizeTextForImpeller(label.Text), label.Font, textBrush, textRect);
            }
            else if (control is System.Windows.Forms.Button button)
            {
                DrawButton(button, g);
            }
            else if (control is TextBoxBase textBoxBase)
            {
                DrawTextBox(textBoxBase, g);
            }
            else if (control is CheckBox checkBox)
            {
                DrawCheckBox(checkBox, g);
            }
            else if (control is RadioButton radioButton)
            {
                DrawRadioButton(radioButton, g);
            }
            else if (control is GroupBox groupBox)
            {
                DrawGroupBox(groupBox, g);
            }
            else if (control is ListBox listBox)
            {
                DrawListBox(listBox, g);
            }
            else if (control is ComboBox comboBox)
            {
                DrawComboBox(comboBox, g);
            }
            else if (control is TreeView treeView)
            {
                DrawTreeView(treeView, g);
            }
            else if (control is ListView listView)
            {
                DrawListView(listView, g);
            }
            else if (control is DataGridView dataGridView)
            {
                DrawDataGridView(dataGridView, g);
            }
            else if (control is ScrollBar scrollBar)
            {
                DrawScrollBar(scrollBar, g);
            }
            else if (control is MenuStrip menuStrip)
            {
                DrawMenuStrip(menuStrip, g);
            }
            else if (control is StatusStrip statusStrip)
            {
                DrawStatusStrip(statusStrip, g);
            }
            else if (control is TabControl tabControl)
            {
                DrawTabHeaders(tabControl, g);
            }
            else
            {
                handled = false;
            }

            if (!handled && IsUserPaintControl(control))
            {
                using var paintArgs = new PaintEventArgs(g, clipRect);
                control.InvokePaintBackgroundInternal(paintArgs);
                control.InvokePaintInternal(paintArgs);
            }

            // Impeller-first clean-room mode: do not call Win32/GDI-dependent paint
            // dispatch for now. We recurse and paint primitive surfaces through backend.
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Paint ERROR] {control.GetType().Name}: {ex}");
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
            if (RequiresChildClip(control, child))
            {
                g.IntersectClip(childClip);
            }

            PaintControlTree(child, g, childClip, isRoot: false);

            g.Restore(state);
        }

        if (isRoot)
            s_firstPaint = false;
    }

    private static void DrawButton(System.Windows.Forms.Button button, System.Drawing.Graphics g)
    {
        bool pressed = s_pressedControls.Contains(button);
        bool customBackColor = !IsDefaultControlBackColor(button.BackColor);

        System.Drawing.Color face;
        System.Drawing.Color border;
        System.Drawing.Color text;
        if (!button.Enabled)
        {
            face = System.Drawing.Color.FromArgb(239, 239, 239);
            border = System.Drawing.Color.FromArgb(204, 204, 204);
            text = System.Drawing.Color.FromArgb(131, 131, 131);
        }
        else
        {
            face = customBackColor ? NormalizeSystemColor(button.BackColor) : System.Drawing.Color.FromArgb(225, 225, 225);
            border = pressed
                ? System.Drawing.Color.FromArgb(0, 84, 153)
                : System.Drawing.Color.FromArgb(173, 173, 173);
            text = button.ForeColor.A == 0
                ? System.Drawing.SystemColors.ControlText
                : button.ForeColor;

            if (pressed)
            {
                face = customBackColor
                    ? Darken(face, 28)
                    : System.Drawing.Color.FromArgb(204, 228, 247);
            }
        }

        using var fill = new System.Drawing.SolidBrush(face);
        g.FillRectangle(fill, 0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height));

        using var borderPen = new System.Drawing.Pen(border);
        g.DrawRectangle(borderPen, 0, 0, Math.Max(1, button.Width) - 1, Math.Max(1, button.Height) - 1);

        if (!string.IsNullOrEmpty(button.Text))
        {
            using var textBrush = new System.Drawing.SolidBrush(text);
            var textRect = new System.Drawing.RectangleF(0, 0, Math.Max(1, button.Width), Math.Max(1, button.Height));
            using var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center,
                Trimming = System.Drawing.StringTrimming.EllipsisCharacter,
            };
            g.DrawString(SanitizeTextForImpeller(button.Text), button.Font, textBrush, textRect, sf);
        }
    }

    private static System.Drawing.Color Darken(System.Drawing.Color color, int amount)
        => System.Drawing.Color.FromArgb(
            color.A,
            Math.Max(0, color.R - amount),
            Math.Max(0, color.G - amount),
            Math.Max(0, color.B - amount));

    private static bool IsDefaultControlBackColor(System.Drawing.Color color)
    {
        if (color.A == 0)
        {
            return true;
        }

        if (color.IsSystemColor)
        {
            var knownColor = color.ToKnownColor();
            return knownColor is System.Drawing.KnownColor.Control or System.Drawing.KnownColor.ButtonFace;
        }

        return color.ToArgb() == s_managedControlColor.ToArgb();
    }

    private static System.Drawing.Color NormalizeSystemColor(System.Drawing.Color color)
    {
        if (!color.IsSystemColor)
        {
            return color;
        }

        return color.ToKnownColor() switch
        {
            System.Drawing.KnownColor.Control or System.Drawing.KnownColor.ButtonFace => s_managedControlColor,
            System.Drawing.KnownColor.ControlLight => s_managedControlLightColor,
            System.Drawing.KnownColor.ControlLightLight => System.Drawing.Color.White,
            System.Drawing.KnownColor.ControlDark => s_managedControlDarkColor,
            System.Drawing.KnownColor.ControlDarkDark => s_managedControlDarkDarkColor,
            System.Drawing.KnownColor.Menu or System.Drawing.KnownColor.MenuBar => s_managedMenuColor,
            System.Drawing.KnownColor.Window => System.Drawing.Color.White,
            _ => color,
        };
    }

    private static void DrawTextBox(TextBoxBase textBox, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(textBox.BackColor.A == 0 ? System.Drawing.SystemColors.Window : textBox.BackColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, textBox.Width), Math.Max(1, textBox.Height));
        DrawSimpleBorder(g, 0, 0, textBox.Width, textBox.Height, System.Drawing.Color.FromArgb(170, 170, 170));

        string text = textBox.Text;
        if (textBox is TextBox tb && tb.PasswordChar != '\0' && text.Length > 0)
        {
            text = new string(tb.PasswordChar, text.Length);
        }

        if (text.Length == 0)
        {
            DrawTextBoxCaret(textBox, g, 0);
            return;
        }

        int selectionStart = Math.Clamp(textBox.SelectionStart, 0, text.Length);
        int selectionLength = Math.Clamp(textBox.SelectionLength, 0, text.Length - selectionStart);
        float charWidth = EstimateTextBoxCharWidth(textBox);
        if (selectionLength > 0 && PlatformApi.Input.GetFocus() == (HWND)(nint)textBox.Handle)
        {
            using var selectionBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
            float selectionX = ManagedTextPadding + selectionStart * charWidth;
            float selectionW = Math.Max(2, selectionLength * charWidth);
            g.FillRectangle(selectionBrush, selectionX, 3, Math.Min(selectionW, Math.Max(1, textBox.Width - selectionX - ManagedTextPadding)), Math.Max(1, textBox.Height - 6));
        }

        using var textBrush = new System.Drawing.SolidBrush(textBox.ForeColor.A == 0 ? System.Drawing.SystemColors.WindowText : textBox.ForeColor);
        var rect = new System.Drawing.RectangleF(
            ManagedTextPadding,
            Math.Max(1, (textBox.Height - textBox.Font.Height) / 2),
            Math.Max(1, textBox.Width - ManagedTextPadding * 2),
            Math.Max(1, textBox.Height - 2));
        g.DrawString(SanitizeTextForImpeller(text), textBox.Font, textBrush, rect);
        DrawTextBoxCaret(textBox, g, selectionStart + selectionLength);
    }

    private static void DrawTextBoxCaret(TextBoxBase textBox, System.Drawing.Graphics g, int caretIndex)
    {
        if (PlatformApi.Input.GetFocus() != (HWND)(nint)textBox.Handle)
        {
            return;
        }

        caretIndex = Math.Clamp(caretIndex, 0, textBox.Text.Length);
        float charWidth = EstimateTextBoxCharWidth(textBox);
        float x = ManagedTextPadding + caretIndex * charWidth;
        if (x >= textBox.Width - ManagedTextPadding)
        {
            x = Math.Max(ManagedTextPadding, textBox.Width - ManagedTextPadding - 1);
        }

        using var caretPen = new System.Drawing.Pen(System.Drawing.Color.Black, 1);
        g.DrawLine(caretPen, x, 4, x, Math.Max(5, textBox.Height - 5));
    }

    private static float EstimateTextBoxCharWidth(TextBoxBase textBox)
        => Math.Max(5f, textBox.Font.Size * 0.62f);

    private static void DrawCheckBox(CheckBox checkBox, System.Drawing.Graphics g)
    {
        int glyphY = Math.Max(0, (checkBox.Height - ManagedCheckGlyphSize) / 2);
        using var boxBg = new System.Drawing.SolidBrush(checkBox.Enabled ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(235, 235, 235));
        g.FillRectangle(boxBg, 0, glyphY, ManagedCheckGlyphSize, ManagedCheckGlyphSize);
        DrawSimpleBorder(g, 0, glyphY, ManagedCheckGlyphSize, ManagedCheckGlyphSize, System.Drawing.Color.FromArgb(90, 90, 90));

        if (checkBox.CheckState == CheckState.Checked)
        {
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(20, 110, 20), 2);
            g.DrawLine(pen, 3, glyphY + 7, 6, glyphY + 10);
            g.DrawLine(pen, 6, glyphY + 10, 12, glyphY + 3);
        }
        else if (checkBox.CheckState == CheckState.Indeterminate)
        {
            using var mark = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(90, 90, 90));
            g.FillRectangle(mark, 3, glyphY + 6, ManagedCheckGlyphSize - 6, 3);
        }

        DrawGlyphLabel(checkBox, g, ManagedCheckGlyphSize + 6);
    }

    private static void DrawRadioButton(RadioButton radioButton, System.Drawing.Graphics g)
    {
        int glyphY = Math.Max(0, (radioButton.Height - ManagedCheckGlyphSize) / 2);
        using var boxBg = new System.Drawing.SolidBrush(radioButton.Enabled ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(235, 235, 235));
        g.FillEllipse(boxBg, 0, glyphY, ManagedCheckGlyphSize, ManagedCheckGlyphSize);
        using var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(90, 90, 90));
        g.DrawEllipse(border, 0, glyphY, ManagedCheckGlyphSize, ManagedCheckGlyphSize);

        if (radioButton.Checked)
        {
            using var dot = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
            g.FillEllipse(dot, 4, glyphY + 4, ManagedCheckGlyphSize - 8, ManagedCheckGlyphSize - 8);
        }

        DrawGlyphLabel(radioButton, g, ManagedCheckGlyphSize + 6);
    }

    private static void DrawGlyphLabel(Control control, System.Drawing.Graphics g, int textX)
    {
        if (string.IsNullOrEmpty(control.Text))
        {
            return;
        }

        using var brush = new System.Drawing.SolidBrush(control.Enabled
            ? (control.ForeColor.A == 0 ? System.Drawing.SystemColors.ControlText : control.ForeColor)
            : System.Drawing.Color.FromArgb(130, 130, 130));
        var rect = new System.Drawing.RectangleF(
            textX,
            Math.Max(0, (control.Height - control.Font.Height) / 2),
            Math.Max(1, control.Width - textX),
            Math.Max(1, control.Height));
        g.DrawString(SanitizeTextForImpeller(control.Text), control.Font, brush, rect);
    }

    private static void DrawGroupBox(GroupBox groupBox, System.Drawing.Graphics g)
    {
        int titleWidth = Math.Min(Math.Max(1, groupBox.Width - 20), Math.Max(24, groupBox.Text.Length * 8 + 12));
        int lineY = Math.Max(8, groupBox.Font.Height / 2 + 2);

        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 150, 140));
        g.DrawLine(pen, 0, lineY, 8, lineY);
        g.DrawLine(pen, titleWidth, lineY, Math.Max(titleWidth, groupBox.Width - 1), lineY);
        g.DrawLine(pen, 0, lineY, 0, Math.Max(lineY, groupBox.Height - 1));
        g.DrawLine(pen, groupBox.Width - 1, lineY, groupBox.Width - 1, Math.Max(lineY, groupBox.Height - 1));
        g.DrawLine(pen, 0, groupBox.Height - 1, Math.Max(0, groupBox.Width - 1), groupBox.Height - 1);

        if (!string.IsNullOrEmpty(groupBox.Text))
        {
            using var brush = new System.Drawing.SolidBrush(groupBox.ForeColor.A == 0 ? System.Drawing.SystemColors.ControlText : groupBox.ForeColor);
            g.DrawString(SanitizeTextForImpeller(groupBox.Text), groupBox.Font, brush, new System.Drawing.PointF(10, 0));
        }
    }

    private static void DrawListBox(ListBox listBox, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(listBox.BackColor.A == 0 ? System.Drawing.SystemColors.Window : listBox.BackColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, listBox.Width), Math.Max(1, listBox.Height));
        DrawSimpleBorder(g, 0, 0, listBox.Width, listBox.Height, System.Drawing.Color.FromArgb(160, 160, 160));

        int itemHeight = Math.Max(ManagedListItemHeight, listBox.ItemHeight);
        int topIndex = 0;
        try
        {
            topIndex = Math.Max(0, listBox.TopIndex);
        }
        catch
        {
        }

        int visibleCount = Math.Max(1, (listBox.Height - 2) / itemHeight);
        for (int visible = 0; visible < visibleCount; visible++)
        {
            int itemIndex = topIndex + visible;
            if (itemIndex < 0 || itemIndex >= listBox.Items.Count)
            {
                break;
            }

            int y = 1 + visible * itemHeight;
            bool selected = listBox.SelectedIndices.Contains(itemIndex);
            DrawListItemBackground(g, selected, 1, y, Math.Max(1, listBox.Width - 2), itemHeight);
            DrawListText(g, listBox.GetItemText(listBox.Items[itemIndex]), listBox.Font, selected ? System.Drawing.Color.White : listBox.ForeColor, 5, y + 3, listBox.Width - 8, itemHeight);
        }
    }

    private static void DrawComboBox(ComboBox comboBox, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(comboBox.BackColor.A == 0 ? System.Drawing.SystemColors.Window : comboBox.BackColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, comboBox.Width), Math.Max(1, comboBox.Height));
        DrawSimpleBorder(g, 0, 0, comboBox.Width, comboBox.Height, System.Drawing.Color.FromArgb(150, 150, 150));

        string text = (comboBox.SelectedIndex >= 0 && comboBox.SelectedIndex < comboBox.Items.Count
            ? comboBox.GetItemText(comboBox.Items[comboBox.SelectedIndex])
            : comboBox.Text) ?? string.Empty;
        DrawListText(g, text, comboBox.Font, comboBox.ForeColor, 5, Math.Max(1, (comboBox.Height - comboBox.Font.Height) / 2), Math.Max(1, comboBox.Width - 28), comboBox.Height);

        int buttonX = Math.Max(0, comboBox.Width - 22);
        using var buttonBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 235, 235));
        g.FillRectangle(buttonBg, buttonX, 1, 21, Math.Max(1, comboBox.Height - 2));
        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(80, 80, 80));
        g.DrawLine(pen, buttonX + 6, comboBox.Height / 2 - 2, buttonX + 11, comboBox.Height / 2 + 3);
        g.DrawLine(pen, buttonX + 11, comboBox.Height / 2 + 3, buttonX + 16, comboBox.Height / 2 - 2);
    }

    private static void DrawTreeView(TreeView treeView, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(treeView.BackColor.A == 0 ? System.Drawing.SystemColors.Window : treeView.BackColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, treeView.Width), Math.Max(1, treeView.Height));
        DrawSimpleBorder(g, 0, 0, treeView.Width, treeView.Height, System.Drawing.Color.FromArgb(170, 170, 170));

        var rows = new List<(TreeNode Node, int Depth)>();
        foreach (TreeNode node in treeView.Nodes)
        {
            AddVisibleTreeRows(node, 0, rows, int.MaxValue);
        }

        TreeNode? selected = treeView.SelectedNode;
        int visibleRows = Math.Max(1, (treeView.Height - 2) / ManagedTreeItemHeight);
        int topRow = Math.Clamp(
            s_managedTreeViewTopRow.TryGetValue(treeView, out int storedTopRow) ? storedTopRow : 0,
            0,
            Math.Max(0, rows.Count - visibleRows));
        for (int visible = 0; visible < visibleRows; visible++)
        {
            int i = topRow + visible;
            if (i < 0 || i >= rows.Count)
            {
                break;
            }

            int y = 1 + visible * ManagedTreeItemHeight;
            if (y > treeView.Height - ManagedTreeItemHeight)
            {
                break;
            }

            var (node, depth) = rows[i];
            bool isSelected = ReferenceEquals(node, selected);
            DrawListItemBackground(g, isSelected, 1, y, Math.Max(1, treeView.Width - 2), ManagedTreeItemHeight);
            int x = 4 + depth * 18;
            if (node.Nodes.Count > 0)
            {
                DrawSimpleBorder(g, x, y + 5, 10, 10, System.Drawing.Color.FromArgb(120, 120, 120));
                using var glyphPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(60, 60, 60));
                g.DrawLine(glyphPen, x + 2, y + 10, x + 8, y + 10);
                if (!node.IsExpanded)
                {
                    g.DrawLine(glyphPen, x + 5, y + 7, x + 5, y + 13);
                }
            }

            DrawListText(g, node.Text, treeView.Font, isSelected ? System.Drawing.Color.White : treeView.ForeColor, x + 16, y + 3, Math.Max(1, treeView.Width - x - 18), ManagedTreeItemHeight);
        }
    }

    private static void DrawListView(ListView listView, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(listView.BackColor.A == 0 ? System.Drawing.SystemColors.Window : listView.BackColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, listView.Width), Math.Max(1, listView.Height));
        DrawSimpleBorder(g, 0, 0, listView.Width, listView.Height, System.Drawing.Color.FromArgb(170, 170, 170));

        int x = 1;
        using var headerBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 235, 235));
        g.FillRectangle(headerBg, 1, 1, Math.Max(1, listView.Width - 2), ManagedListViewHeaderHeight);
        foreach (ColumnHeader column in listView.Columns)
        {
            int width = Math.Max(40, column.Width);
            DrawSimpleBorder(g, x, 1, width, ManagedListViewHeaderHeight, System.Drawing.Color.FromArgb(190, 190, 190));
            DrawListText(g, column.Text, listView.Font, System.Drawing.SystemColors.ControlText, x + 4, 4, width - 8, ManagedListViewHeaderHeight);
            x += width;
            if (x > listView.Width)
            {
                break;
            }
        }

        int rowY = ManagedListViewHeaderHeight + 1;
        int visibleRows = Math.Max(1, (listView.Height - rowY) / ManagedListItemHeight);
        int topRow = Math.Clamp(
            s_managedListViewTopRow.TryGetValue(listView, out int storedTopRow) ? storedTopRow : 0,
            0,
            Math.Max(0, listView.Items.Count - visibleRows));
        for (int visible = 0; visible < visibleRows && rowY < listView.Height - ManagedListItemHeight; visible++)
        {
            int row = topRow + visible;
            if (row < 0 || row >= listView.Items.Count)
            {
                break;
            }

            ListViewItem item = listView.Items[row];
            if (item is null)
            {
                continue;
            }

            bool selected = item.Selected || (s_managedListViewSelection.TryGetValue(listView, out int selectedIndex) && selectedIndex == row);
            DrawListItemBackground(g, selected, 1, rowY, Math.Max(1, listView.Width - 2), ManagedListItemHeight);

            x = 1;
            int subItemCount = Math.Max(1, item.ManagedSubItemCount);
            for (int col = 0; col < Math.Max(1, listView.Columns.Count); col++)
            {
                int width = col < listView.Columns.Count ? Math.Max(40, listView.Columns[col].Width) : listView.Width - 2;
                string text = col < subItemCount ? item.GetManagedSubItemText(col) : string.Empty;
                DrawListText(g, text, listView.Font, selected ? System.Drawing.Color.White : listView.ForeColor, x + 4, rowY + 3, width - 8, ManagedListItemHeight);
                x += width;
                if (x > listView.Width)
                {
                    break;
                }
            }

            rowY += ManagedListItemHeight;
        }
    }

    private static void DrawDataGridView(DataGridView dataGridView, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(dataGridView.BackgroundColor.A == 0 ? System.Drawing.SystemColors.Window : dataGridView.BackgroundColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, dataGridView.Width), Math.Max(1, dataGridView.Height));
        DrawSimpleBorder(g, 0, 0, dataGridView.Width, dataGridView.Height, System.Drawing.Color.FromArgb(170, 170, 170));

        int rowHeaderWidth = dataGridView.RowHeadersVisible ? Math.Max(36, dataGridView.RowHeadersWidth) : 0;
        int x = rowHeaderWidth;
        using var headerBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(235, 235, 235));
        g.FillRectangle(headerBg, 1, 1, Math.Max(1, dataGridView.Width - 2), ManagedDataGridHeaderHeight);

        foreach (DataGridViewColumn column in dataGridView.Columns)
        {
            if (!column.Visible)
            {
                continue;
            }

            int width = Math.Max(40, column.Width);
            DrawSimpleBorder(g, x, 1, width, ManagedDataGridHeaderHeight, System.Drawing.Color.FromArgb(190, 190, 190));
            DrawListText(g, column.HeaderText, dataGridView.Font, System.Drawing.SystemColors.ControlText, x + 4, 4, width - 8, ManagedDataGridHeaderHeight);
            x += width;
            if (x > dataGridView.Width)
            {
                break;
            }
        }

        int rowY = ManagedDataGridHeaderHeight + 1;
        int visibleRows = Math.Max(1, (dataGridView.Height - rowY) / ManagedDataGridRowHeight);
        int topRow = Math.Clamp(
            s_managedDataGridViewTopRow.TryGetValue(dataGridView, out int storedTopRow) ? storedTopRow : 0,
            0,
            Math.Max(0, GetManagedDataGridViewRowCount(dataGridView) - visibleRows));
        for (int visible = 0; visible < visibleRows; visible++)
        {
            int i = topRow + visible;
            if (i < 0 || i >= dataGridView.Rows.Count)
            {
                break;
            }

            DataGridViewRow row = dataGridView.Rows[i];
            if (row.IsNewRow)
            {
                continue;
            }

            bool selected = row.Selected
                || (s_managedDataGridViewSelection.TryGetValue(dataGridView, out int selectedRow) && selectedRow == i);
            DrawListItemBackground(g, selected, 1, rowY, Math.Max(1, dataGridView.Width - 2), ManagedDataGridRowHeight);
            x = rowHeaderWidth;
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                if (!column.Visible)
                {
                    continue;
                }

                int width = Math.Max(40, column.Width);
                string text = string.Empty;
                if (column.Index >= 0 && column.Index < row.Cells.Count)
                {
                    text = row.Cells[column.Index].FormattedValue?.ToString() ?? row.Cells[column.Index].Value?.ToString() ?? string.Empty;
                }

                DrawSimpleBorder(g, x, rowY, width, ManagedDataGridRowHeight, System.Drawing.Color.FromArgb(220, 220, 220));
                DrawListText(g, text, dataGridView.Font, selected ? System.Drawing.Color.White : dataGridView.ForeColor, x + 4, rowY + 3, width - 8, ManagedDataGridRowHeight);
                x += width;
                if (x > dataGridView.Width)
                {
                    break;
                }
            }

            rowY += ManagedDataGridRowHeight;
        }
    }

    private static void DrawScrollBar(ScrollBar scrollBar, System.Drawing.Graphics g)
    {
        bool vertical = scrollBar is VScrollBar;
        var bounds = new System.Drawing.Rectangle(0, 0, Math.Max(1, scrollBar.Width), Math.Max(1, scrollBar.Height));

        using var trackBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(238, 238, 238));
        g.FillRectangle(trackBrush, bounds);
        DrawSimpleBorder(g, bounds.X, bounds.Y, bounds.Width, bounds.Height, System.Drawing.Color.FromArgb(170, 170, 170));

        int arrow = Math.Min(ManagedScrollBarArrowLength, vertical ? bounds.Height / 2 : bounds.Width / 2);
        var firstArrow = vertical
            ? new System.Drawing.Rectangle(0, 0, bounds.Width, arrow)
            : new System.Drawing.Rectangle(0, 0, arrow, bounds.Height);
        var lastArrow = vertical
            ? new System.Drawing.Rectangle(0, Math.Max(0, bounds.Height - arrow), bounds.Width, arrow)
            : new System.Drawing.Rectangle(Math.Max(0, bounds.Width - arrow), 0, arrow, bounds.Height);

        using var arrowBrush = new System.Drawing.SolidBrush(scrollBar.Enabled
            ? System.Drawing.Color.FromArgb(224, 224, 224)
            : System.Drawing.Color.FromArgb(245, 245, 245));
        g.FillRectangle(arrowBrush, firstArrow);
        g.FillRectangle(arrowBrush, lastArrow);
        DrawSimpleBorder(g, firstArrow.X, firstArrow.Y, firstArrow.Width, firstArrow.Height, System.Drawing.Color.FromArgb(185, 185, 185));
        DrawSimpleBorder(g, lastArrow.X, lastArrow.Y, lastArrow.Width, lastArrow.Height, System.Drawing.Color.FromArgb(185, 185, 185));

        using var glyphPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(70, 70, 70), 1.5f);
        DrawScrollBarArrowGlyph(g, glyphPen, firstArrow, vertical, decrement: true);
        DrawScrollBarArrowGlyph(g, glyphPen, lastArrow, vertical, decrement: false);

        var thumb = GetScrollBarThumbRectangle(scrollBar);
        if (thumb.Width > 0 && thumb.Height > 0)
        {
            using var thumbBrush = new System.Drawing.SolidBrush(scrollBar.Enabled
                ? System.Drawing.Color.FromArgb(178, 178, 178)
                : System.Drawing.Color.FromArgb(215, 215, 215));
            g.FillRectangle(thumbBrush, thumb);
            DrawSimpleBorder(g, thumb.X, thumb.Y, thumb.Width, thumb.Height, System.Drawing.Color.FromArgb(120, 120, 120));
        }
    }

    private static void DrawScrollBarArrowGlyph(
        System.Drawing.Graphics g,
        System.Drawing.Pen pen,
        System.Drawing.Rectangle rect,
        bool vertical,
        bool decrement)
    {
        if (rect.Width <= 4 || rect.Height <= 4)
        {
            return;
        }

        int cx = rect.Left + rect.Width / 2;
        int cy = rect.Top + rect.Height / 2;
        if (vertical)
        {
            int direction = decrement ? -1 : 1;
            g.DrawLine(pen, cx - 4, cy - direction * 2, cx, cy + direction * 3);
            g.DrawLine(pen, cx, cy + direction * 3, cx + 4, cy - direction * 2);
        }
        else
        {
            int direction = decrement ? -1 : 1;
            g.DrawLine(pen, cx - direction * 2, cy - 4, cx + direction * 3, cy);
            g.DrawLine(pen, cx + direction * 3, cy, cx - direction * 2, cy + 4);
        }
    }

    private static System.Drawing.Rectangle GetScrollBarThumbRectangle(ScrollBar scrollBar)
    {
        bool vertical = scrollBar is VScrollBar;
        int length = Math.Max(1, vertical ? scrollBar.Height : scrollBar.Width);
        int thickness = Math.Max(1, vertical ? scrollBar.Width : scrollBar.Height);
        int arrow = Math.Min(ManagedScrollBarArrowLength, length / 2);
        int trackStart = arrow;
        int trackLength = Math.Max(0, length - arrow * 2);
        if (trackLength <= 0)
        {
            return System.Drawing.Rectangle.Empty;
        }

        int minimum = scrollBar.Minimum;
        int maximum = Math.Max(minimum, scrollBar.Maximum);
        int largeChange = Math.Max(1, scrollBar.LargeChange);
        int range = Math.Max(1, maximum - minimum + 1);
        int thumbLength = Math.Clamp((int)MathF.Round(trackLength * (largeChange / (float)range)), ManagedScrollBarMinThumbLength, trackLength);
        int maxValue = GetScrollBarMaximumValue(scrollBar);
        float ratio = maxValue <= minimum
            ? 0f
            : (Math.Clamp(scrollBar.Value, minimum, maxValue) - minimum) / (float)(maxValue - minimum);
        int thumbStart = trackStart + (int)MathF.Round((trackLength - thumbLength) * ratio);

        return vertical
            ? new System.Drawing.Rectangle(1, thumbStart, Math.Max(1, thickness - 2), thumbLength)
            : new System.Drawing.Rectangle(thumbStart, 1, thumbLength, Math.Max(1, thickness - 2));
    }

    private static void DrawStatusStrip(StatusStrip statusStrip, System.Drawing.Graphics g)
    {
        using var bg = new System.Drawing.SolidBrush(s_managedControlColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, statusStrip.Width), Math.Max(1, statusStrip.Height));
        using var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(204, 206, 219));
        g.DrawLine(border, 0, 0, Math.Max(1, statusStrip.Width), 0);

        int x = 8;
        foreach (ToolStripItem item in statusStrip.Items)
        {
            string text = SanitizeTextForImpeller(item.Text);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(40, 40, 40));
            g.DrawString(text, statusStrip.Font, brush, new System.Drawing.PointF(x, 4));
            x += Math.Max(80, text.Length * 7 + 24);
        }
    }

    private static void DrawSimpleBorder(System.Drawing.Graphics g, int x, int y, int width, int height, System.Drawing.Color color)
    {
        using var pen = new System.Drawing.Pen(color);
        g.DrawRectangle(pen, x, y, Math.Max(0, width - 1), Math.Max(0, height - 1));
    }

    private static void DrawListItemBackground(System.Drawing.Graphics g, bool selected, int x, int y, int width, int height)
    {
        if (!selected)
        {
            return;
        }

        using var selectedBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
        g.FillRectangle(selectedBg, x, y, width, height);
    }

    private static void DrawListText(
        System.Drawing.Graphics g,
        string? text,
        System.Drawing.Font font,
        System.Drawing.Color color,
        int x,
        int y,
        int width,
        int height)
    {
        if (string.IsNullOrEmpty(text) || width <= 0 || height <= 0)
        {
            return;
        }

        using var brush = new System.Drawing.SolidBrush(color.A == 0 ? System.Drawing.SystemColors.ControlText : color);
        using var sf = new System.Drawing.StringFormat
        {
            Trimming = System.Drawing.StringTrimming.EllipsisCharacter,
        };
        g.DrawString(SanitizeTextForImpeller(text), font, brush, new System.Drawing.RectangleF(x, y, width, height), sf);
    }

    private static void AddVisibleTreeRows(TreeNode node, int depth, List<(TreeNode Node, int Depth)> rows, int maxRows)
    {
        if (rows.Count >= maxRows)
        {
            return;
        }

        rows.Add((node, depth));
        if (!node.IsExpanded)
        {
            return;
        }

        foreach (TreeNode child in node.Nodes)
        {
            AddVisibleTreeRows(child, depth + 1, rows, maxRows);
            if (rows.Count >= maxRows)
            {
                return;
            }
        }
    }

    private static void DrawMenuStrip(MenuStrip menuStrip, System.Drawing.Graphics g)
    {
        int height = Math.Max(ManagedMenuItemHeight, menuStrip.Height);
        using var bg = new System.Drawing.SolidBrush(s_managedMenuColor);
        g.FillRectangle(bg, 0, 0, Math.Max(1, menuStrip.Width), Math.Max(1, menuStrip.Height));
        using var bottomLine = new System.Drawing.Pen(System.Drawing.Color.FromArgb(204, 206, 219));
        g.DrawLine(bottomLine, 0, Math.Max(0, height - 1), Math.Max(1, menuStrip.Width), Math.Max(0, height - 1));

        int x = ManagedMenuItemLeft;
        s_managedActiveMenuItems.TryGetValue(menuStrip, out ToolStripItem? activeItem);
        foreach (ToolStripItem item in menuStrip.Items)
        {
            string text = SanitizeTextForImpeller(item.Text);
            if (text.Length == 0)
            {
                continue;
            }

            int width = GetManagedMenuItemWidth(item);
            var itemRect = new System.Drawing.Rectangle(x, 1, width, Math.Max(1, height - 2));
            if (ReferenceEquals(item, activeItem))
            {
                using var activeBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(204, 232, 255));
                g.FillRectangle(activeBg, itemRect);
                DrawSimpleBorder(g, itemRect.X, itemRect.Y, itemRect.Width, itemRect.Height, System.Drawing.Color.FromArgb(0, 120, 215));
            }

            using var brush = new System.Drawing.SolidBrush(item.Enabled
                ? System.Drawing.SystemColors.MenuText
                : System.Drawing.SystemColors.GrayText);
            float textY = Math.Max(1, itemRect.Top + (itemRect.Height - menuStrip.Font.Height) / 2f);
            g.DrawString(text, menuStrip.Font, brush, new System.Drawing.PointF(x + ManagedMenuItemHorizontalPadding, textY));
            x += width + ManagedMenuItemGap;
        }
    }

    private static void DrawManagedMenuDropDown(ImpellerWindowState state, System.Drawing.Graphics g)
    {
        ToolStripMenuItem? menuItem = state.ActiveMenuItem;
        if (menuItem is null || state.ActiveMenuBounds.Width <= 0 || state.ActiveMenuBounds.Height <= 0)
        {
            return;
        }

        var bounds = state.ActiveMenuBounds;
        using var shadow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(45, 0, 0, 0));
        g.FillRectangle(shadow, bounds.X + 3, bounds.Y + 3, bounds.Width, bounds.Height);

        using var bg = new System.Drawing.SolidBrush(s_managedMenuColor);
        g.FillRectangle(bg, bounds);

        using var border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(204, 204, 204));
        g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        int y = bounds.Y;
        foreach (ToolStripItem item in menuItem.DropDownItems)
        {
            var row = new System.Drawing.Rectangle(bounds.X, y, bounds.Width, ManagedMenuDropDownRowHeight);
            if (item is ToolStripSeparator)
            {
                using var line = new System.Drawing.Pen(System.Drawing.Color.FromArgb(210, 210, 210));
                int lineY = row.Top + row.Height / 2;
                g.DrawLine(line, row.Left + 8, lineY, row.Right - 8, lineY);
            }
            else
            {
                using var textBrush = new System.Drawing.SolidBrush(item.Enabled
                    ? System.Drawing.SystemColors.MenuText
                    : System.Drawing.SystemColors.GrayText);
                g.DrawString(SanitizeTextForImpeller(item.Text), menuItem.Font, textBrush, new System.Drawing.PointF(row.Left + 10, row.Top + 3));

                string shortcutText = item is ToolStripMenuItem childMenuItem
                    ? childMenuItem.ShortcutKeyDisplayString ?? string.Empty
                    : string.Empty;
                if (!string.IsNullOrEmpty(shortcutText))
                {
                    using var shortcutBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(90, 90, 90));
                    int shortcutWidth = EstimateTextWidth(shortcutText) + 6;
                    g.DrawString(SanitizeTextForImpeller(shortcutText), menuItem.Font, shortcutBrush, new System.Drawing.PointF(row.Right - shortcutWidth, row.Top + 3));
                }
            }

            y += ManagedMenuDropDownRowHeight;
        }
    }

    private static void DrawManagedComboDropDown(ImpellerWindowState state, System.Drawing.Graphics g)
    {
        ComboBox? comboBox = state.ActiveComboBox;
        if (comboBox is null || state.ActiveComboBounds.Width <= 0 || state.ActiveComboBounds.Height <= 0)
        {
            return;
        }

        var bounds = state.ActiveComboBounds;
        using var shadow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(70, 0, 0, 0));
        g.FillRectangle(shadow, bounds.X + 3, bounds.Y + 3, bounds.Width, bounds.Height);

        using var bg = new System.Drawing.SolidBrush(System.Drawing.Color.White);
        g.FillRectangle(bg, bounds);
        DrawSimpleBorder(g, bounds.X, bounds.Y, bounds.Width, bounds.Height, System.Drawing.Color.FromArgb(90, 90, 90));

        int visibleCount = Math.Min(comboBox.Items.Count, ManagedComboDropDownMaxVisibleItems);
        for (int i = 0; i < visibleCount; i++)
        {
            var row = new System.Drawing.Rectangle(
                bounds.X,
                bounds.Y + i * ManagedComboItemHeight,
                bounds.Width,
                ManagedComboItemHeight);
            bool selected = i == comboBox.SelectedIndex;
            DrawListItemBackground(g, selected, row.X + 1, row.Y + 1, row.Width - 2, row.Height - 2);
            DrawListText(
                g,
                comboBox.GetItemText(comboBox.Items[i]),
                comboBox.Font,
                selected ? System.Drawing.Color.White : comboBox.ForeColor,
                row.X + 5,
                row.Y + 4,
                row.Width - 10,
                row.Height - 2);
        }
    }

    private static void DrawTabHeaders(TabControl tabControl, System.Drawing.Graphics g)
    {
        int width = Math.Max(1, tabControl.Width);
        int height = Math.Max(1, tabControl.Height);
        using var stripBg = new System.Drawing.SolidBrush(s_managedControlColor);
        g.FillRectangle(stripBg, 0, 0, width, ManagedTabHeaderHeight);
        using var contentBg = new System.Drawing.SolidBrush(s_managedControlColor);
        g.FillRectangle(contentBg, 0, ManagedTabHeaderHeight - 1, width, Math.Max(1, height - ManagedTabHeaderHeight + 1));
        using var framePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(172, 172, 172));
        g.DrawRectangle(framePen, 0, ManagedTabHeaderHeight - 1, Math.Max(0, width - 1), Math.Max(0, height - ManagedTabHeaderHeight));

        int x = ManagedTabHeaderLeft;
        int selected = GetSelectedTabIndexSafe(tabControl);
        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            var tab = tabControl.TabPages[i];
            bool isSelected = i == selected;
            var rect = isSelected
                ? new System.Drawing.Rectangle(x, 2, ManagedTabHeaderWidth, ManagedTabHeaderHeight - 1)
                : new System.Drawing.Rectangle(x, 5, ManagedTabHeaderWidth, ManagedTabHeaderHeight - 6);

            using var fill = new System.Drawing.SolidBrush(isSelected
                ? s_managedControlColor
                : System.Drawing.Color.FromArgb(236, 236, 236));
            g.FillRectangle(fill, rect);

            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(172, 172, 172));
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            if (isSelected)
            {
                using var erase = new System.Drawing.Pen(s_managedControlColor, 2);
                g.DrawLine(erase, rect.Left + 1, ManagedTabHeaderHeight - 1, rect.Right - 2, ManagedTabHeaderHeight - 1);
            }

            using var brush = new System.Drawing.SolidBrush(System.Drawing.SystemColors.ControlText);
            using var sf = new System.Drawing.StringFormat
            {
                Alignment = System.Drawing.StringAlignment.Center,
                LineAlignment = System.Drawing.StringAlignment.Center,
                Trimming = System.Drawing.StringTrimming.EllipsisCharacter,
            };
            g.DrawString(SanitizeTextForImpeller(tab.Text), tabControl.Font, brush, rect, sf);
            x += ManagedTabHeaderWidth + ManagedTabHeaderGap;
            if (x >= width - 40)
            {
                break;
            }
        }
    }

    private static int GetSelectedTabIndexSafe(TabControl tabControl)
    {
        try
        {
            int index = tabControl.SelectedIndex;
            if (index >= 0 && index < tabControl.TabPages.Count)
            {
                return index;
            }
        }
        catch
        {
        }

        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            if (tabControl.TabPages[i].Visible)
            {
                return i;
            }
        }

        return tabControl.TabPages.Count > 0 ? 0 : -1;
    }

    private static void SelectTabSafe(TabControl tabControl, int index)
    {
        if (index < 0 || index >= tabControl.TabPages.Count)
        {
            return;
        }

        try
        {
            if (tabControl.SelectedIndex != index)
            {
                tabControl.SelectedIndex = index;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TabSelectSafe] SelectedIndex failed: {ex.Message}");
            ApplyManagedTabSelectionFallback(tabControl, index);
        }

        tabControl.Invalidate();
    }

    private static void ApplyManagedTabSelectionFallback(TabControl tabControl, int index)
    {
        System.Drawing.Rectangle displayRectangle = tabControl.DisplayRectangle;

        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            TabPage page = tabControl.TabPages[i];
            bool visible = i == index;
            if (visible)
            {
                page.SuspendLayout();
                try
                {
                    page.Bounds = displayRectangle;
                    page.Visible = true;
                }
                finally
                {
                    page.ResumeLayout(false);
                }
            }
            else
            {
                page.Visible = false;
            }
        }
    }

    private static int GetManagedTabIndexAtPoint(TabControl tabControl, System.Drawing.Point clientPoint)
    {
        if (clientPoint.Y < 0 || clientPoint.Y >= ManagedTabHeaderHeight)
        {
            return -1;
        }

        int x = ManagedTabHeaderLeft;
        int width = Math.Max(1, tabControl.Width);
        for (int i = 0; i < tabControl.TabPages.Count; i++)
        {
            var rect = new System.Drawing.Rectangle(x, 2, ManagedTabHeaderWidth, ManagedTabHeaderHeight - 4);
            if (rect.Contains(clientPoint))
            {
                return i;
            }

            x += ManagedTabHeaderWidth + ManagedTabHeaderGap;
            if (x >= width - 40)
            {
                break;
            }
        }

        return -1;
    }

    private static ToolStripItem? GetManagedMenuItemAtPoint(ToolStrip toolStrip, System.Drawing.Point clientPoint)
    {
        if (clientPoint.Y < 0 || clientPoint.Y >= Math.Max(ManagedMenuItemHeight, toolStrip.Height))
        {
            return null;
        }

        int x = ManagedMenuItemLeft;
        foreach (ToolStripItem item in toolStrip.Items)
        {
            if (!item.Available)
            {
                continue;
            }

            int width = GetManagedMenuItemWidth(item);
            var rect = new System.Drawing.Rectangle(x, 0, width, Math.Max(ManagedMenuItemHeight, toolStrip.Height));
            if (rect.Contains(clientPoint))
            {
                return item;
            }

            x += width + ManagedMenuItemGap;
        }

        return null;
    }

    private static int GetManagedMenuItemWidth(ToolStripItem item)
        => Math.Max(
            ManagedMenuItemMinWidth,
            EstimateTextWidth(item.Text) + ManagedMenuItemHorizontalPadding * 2);

    private static int GetManagedMenuItemOffset(ToolStrip toolStrip, ToolStripItem target)
    {
        int x = ManagedMenuItemLeft;
        foreach (ToolStripItem item in toolStrip.Items)
        {
            if (ReferenceEquals(item, target))
            {
                return x;
            }

            if (item.Available)
            {
                x += GetManagedMenuItemWidth(item) + ManagedMenuItemGap;
            }
        }

        return x;
    }

    private static int GetManagedMenuDropDownWidth(ToolStripMenuItem menuItem)
    {
        int width = ManagedMenuDropDownMinWidth;
        foreach (ToolStripItem item in menuItem.DropDownItems)
        {
            if (item is ToolStripSeparator)
            {
                continue;
            }

            string shortcutText = item is ToolStripMenuItem childMenuItem
                ? childMenuItem.ShortcutKeyDisplayString ?? string.Empty
                : string.Empty;
            int desired = 22 + EstimateTextWidth(item.Text) + (string.IsNullOrEmpty(shortcutText) ? 0 : 24 + EstimateTextWidth(shortcutText));
            width = Math.Max(width, desired);
        }

        return Math.Min(width, 260);
    }

    private static int EstimateTextWidth(string? text)
        => string.IsNullOrEmpty(text) ? 0 : SanitizeTextForImpeller(text).Length * 7;

    private void ShowManagedMenuDropDown(HWND handle, ToolStrip toolStrip, ToolStripMenuItem menuItem, System.Drawing.Point clientPoint)
    {
        if (!_windows.TryGetValue(handle, out var state))
        {
            return;
        }

        var origin = GetRootRelativeLocation(toolStrip);
        int dropDownX = origin.X + GetManagedMenuItemOffset(toolStrip, menuItem);
        int dropDownY = origin.Y + Math.Max(ManagedMenuItemHeight, toolStrip.Height);
        int rowCount = Math.Max(1, menuItem.DropDownItems.Count);

        if (Control.FromHandle(state.ActiveMenuOwner) is ToolStrip previousOwner
            && !ReferenceEquals(previousOwner, toolStrip))
        {
            s_managedActiveMenuItems.Remove(previousOwner);
        }

        state.ActiveMenuItem = menuItem;
        state.ActiveMenuOwner = (HWND)(nint)toolStrip.Handle;
        state.ActiveMenuBounds = new System.Drawing.Rectangle(
            dropDownX,
            dropDownY,
            GetManagedMenuDropDownWidth(menuItem),
            rowCount * ManagedMenuDropDownRowHeight);
        s_managedActiveMenuItems[toolStrip] = menuItem;

        TraceInput($"[ManagedMenuOpen] text={menuItem.Text} bounds={state.ActiveMenuBounds} client={clientPoint}");
        MarkDirty(handle);
    }

    private bool TryHandleManagedMenuOverlayClick(HWND handle, System.Drawing.Point rootPoint)
    {
        if (!_windows.TryGetValue(handle, out var state) || state.ActiveMenuItem is null)
        {
            return false;
        }

        var bounds = state.ActiveMenuBounds;
        ToolStripMenuItem menuItem = state.ActiveMenuItem;
        if (bounds.Contains(rootPoint))
        {
            int row = (rootPoint.Y - bounds.Y) / ManagedMenuDropDownRowHeight;
            ToolStripItem? item = row >= 0 && row < menuItem.DropDownItems.Count
                ? menuItem.DropDownItems[row]
                : null;

            state.ActiveMenuItem = null;
            state.ActiveMenuBounds = System.Drawing.Rectangle.Empty;
            if (Control.FromHandle(state.ActiveMenuOwner) is ToolStrip activeOwner)
            {
                s_managedActiveMenuItems.Remove(activeOwner);
            }

            MarkDirty(handle);

            if (item is not null && item is not ToolStripSeparator && item.Enabled)
            {
                TraceInput($"[ManagedMenuClick] parent={menuItem.Text} item={item.Text}");
                PerformToolStripItemClick(handle, item);
            }

            return true;
        }

        if (Control.FromHandle(state.ActiveMenuOwner) is ToolStrip owner)
        {
            var ownerOrigin = GetRootRelativeLocation(owner);
            var ownerBounds = new System.Drawing.Rectangle(ownerOrigin.X, ownerOrigin.Y, owner.Width, owner.Height);
            if (ownerBounds.Contains(rootPoint))
            {
                return false;
            }
        }

        state.ActiveMenuItem = null;
        state.ActiveMenuBounds = System.Drawing.Rectangle.Empty;
        if (Control.FromHandle(state.ActiveMenuOwner) is ToolStrip previousOwner)
        {
            s_managedActiveMenuItems.Remove(previousOwner);
        }

        MarkDirty(handle);
        return true;
    }

    private void ShowManagedComboDropDown(HWND handle, ComboBox comboBox)
    {
        if (!_windows.TryGetValue(handle, out var state))
        {
            return;
        }

        int visibleCount = Math.Min(comboBox.Items.Count, ManagedComboDropDownMaxVisibleItems);
        if (visibleCount <= 0)
        {
            return;
        }

        var origin = GetRootRelativeLocation(comboBox);
        if (Control.FromHandle(state.ActiveMenuOwner) is ToolStrip previousOwner)
        {
            s_managedActiveMenuItems.Remove(previousOwner);
        }

        state.ActiveMenuItem = null;
        state.ActiveMenuBounds = System.Drawing.Rectangle.Empty;
        state.ActiveComboBox = comboBox;
        state.ActiveComboBounds = new System.Drawing.Rectangle(
            origin.X,
            origin.Y + Math.Max(1, comboBox.Height),
            Math.Max(1, comboBox.Width),
            visibleCount * ManagedComboItemHeight);

        TraceInput($"[ManagedComboOpen] text={comboBox.Text} bounds={state.ActiveComboBounds}");
        MarkDirty(handle);
    }

    private bool TryHandleManagedComboOverlayClick(HWND handle, System.Drawing.Point rootPoint)
    {
        if (!_windows.TryGetValue(handle, out var state) || state.ActiveComboBox is null)
        {
            return false;
        }

        ComboBox comboBox = state.ActiveComboBox;
        var bounds = state.ActiveComboBounds;
        if (bounds.Contains(rootPoint))
        {
            int row = (rootPoint.Y - bounds.Y) / ManagedComboItemHeight;
            state.ActiveComboBox = null;
            state.ActiveComboBounds = System.Drawing.Rectangle.Empty;

            if (row >= 0 && row < comboBox.Items.Count)
            {
                InvokeManagedInteraction(handle, comboBox, () =>
                {
                    comboBox.SelectedIndex = row;
                    TraceInput($"[ManagedComboSelect] index={row} text={comboBox.GetItemText(comboBox.Items[row])}");
                });
            }
            else
            {
                MarkDirty(handle);
            }

            return true;
        }

        var comboOrigin = GetRootRelativeLocation(comboBox);
        var comboBounds = new System.Drawing.Rectangle(comboOrigin.X, comboOrigin.Y, comboBox.Width, comboBox.Height);
        if (comboBounds.Contains(rootPoint))
        {
            return false;
        }

        state.ActiveComboBox = null;
        state.ActiveComboBounds = System.Drawing.Rectangle.Empty;
        MarkDirty(handle);
        return true;
    }

    private void PerformToolStripItemClick(HWND handle, ToolStripItem item)
    {
        Control? invoker = Control.FromHandle(handle) ?? ResolveTopLevelControl(handle);
        if (invoker is null)
        {
            return;
        }

        invoker.BeginInvoke(new Action(() =>
        {
            try
            {
                item.PerformClick();
            }
            catch (Exception ex)
            {
                Application.OnThreadException(ex);
            }
            finally
            {
                MarkDirty(handle);
            }
        }));
    }

    private bool TryHandleManagedTextBoxKeyDown(HWND rootHandle, HWND target, IKeyboard keyboard, Key key)
    {
        if (Control.FromHandle(target) is not TextBoxBase textBox || !textBox.Enabled)
        {
            return false;
        }

        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.Input,
            $"TextBoxKeyDown key={key}");

        bool shift = IsSilkKeyPressed(keyboard, Key.ShiftLeft) || IsSilkKeyPressed(keyboard, Key.ShiftRight);
        bool command = IsSilkKeyPressed(keyboard, Key.ControlLeft)
            || IsSilkKeyPressed(keyboard, Key.ControlRight)
            || IsSilkKeyPressed(keyboard, Key.SuperLeft)
            || IsSilkKeyPressed(keyboard, Key.SuperRight);

        if (command && key == Key.A)
        {
            textBox.SelectAll();
            MarkDirtyDeferred(rootHandle);
            return true;
        }

        int selectionStart = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
        int selectionLength = Math.Clamp(textBox.SelectionLength, 0, textBox.Text.Length - selectionStart);
        int selectionEnd = selectionStart + selectionLength;

        switch (key)
        {
            case Key.Backspace:
                if (textBox.ReadOnly)
                {
                    return true;
                }

                if (selectionLength > 0)
                {
                    ReplaceManagedTextSelection(rootHandle, textBox, string.Empty);
                }
                else if (selectionStart > 0)
                {
                    textBox.Select(selectionStart - 1, 1);
                    ReplaceManagedTextSelection(rootHandle, textBox, string.Empty);
                }

                return true;
            case Key.Delete:
                if (textBox.ReadOnly)
                {
                    return true;
                }

                if (selectionLength > 0)
                {
                    ReplaceManagedTextSelection(rootHandle, textBox, string.Empty);
                }
                else if (selectionStart < textBox.Text.Length)
                {
                    textBox.Select(selectionStart, 1);
                    ReplaceManagedTextSelection(rootHandle, textBox, string.Empty);
                }

                return true;
            case Key.Left:
                MoveManagedTextCaret(textBox, Math.Max(0, selectionLength == 0 ? selectionStart - 1 : selectionStart), shift);
                MarkDirtyDeferred(rootHandle);
                return true;
            case Key.Right:
                MoveManagedTextCaret(textBox, Math.Min(textBox.Text.Length, selectionLength == 0 ? selectionEnd + 1 : selectionEnd), shift);
                MarkDirtyDeferred(rootHandle);
                return true;
            case Key.Home:
                MoveManagedTextCaret(textBox, 0, shift);
                MarkDirtyDeferred(rootHandle);
                return true;
            case Key.End:
                MoveManagedTextCaret(textBox, textBox.Text.Length, shift);
                MarkDirtyDeferred(rootHandle);
                return true;
            default:
                return false;
        }
    }

    private bool TryHandleManagedTextBoxKeyChar(HWND rootHandle, HWND target, IKeyboard keyboard, char character)
    {
        if (Control.FromHandle(target) is not TextBoxBase textBox || !textBox.Enabled)
        {
            return false;
        }

        if (IsSilkKeyPressed(keyboard, Key.ControlLeft)
            || IsSilkKeyPressed(keyboard, Key.ControlRight)
            || IsSilkKeyPressed(keyboard, Key.SuperLeft)
            || IsSilkKeyPressed(keyboard, Key.SuperRight))
        {
            return true;
        }

        if (character == '\b')
        {
            return true;
        }

        if (character is '\r' or '\n')
        {
            if (textBox.Multiline)
            {
                ReplaceManagedTextSelection(rootHandle, textBox, Environment.NewLine);
            }

            return true;
        }

        if (char.IsControl(character))
        {
            return false;
        }

        ReplaceManagedTextSelection(rootHandle, textBox, character.ToString());
        return true;
    }

    private bool TryHandleManagedControlKeyDown(HWND rootHandle, HWND target, IKeyboard keyboard, Key key)
    {
        _ = keyboard;

        if (_windows.TryGetValue(rootHandle, out var state))
        {
            if (state.ActiveComboBox is { } activeComboBox && TryHandleManagedComboKeyDown(rootHandle, state, activeComboBox, key))
            {
                return true;
            }

            if (state.ActiveMenuItem is not null && key == Key.Escape)
            {
                if (Control.FromHandle(state.ActiveMenuOwner) is ToolStrip activeOwner)
                {
                    s_managedActiveMenuItems.Remove(activeOwner);
                }

                state.ActiveMenuItem = null;
                state.ActiveMenuBounds = System.Drawing.Rectangle.Empty;
                MarkDirty(rootHandle);
                return true;
            }
        }

        Control? control = Control.FromHandle(target);
        if (control is null || !control.Enabled)
        {
            return false;
        }

        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.Input,
            $"ManagedKeyDown control={control.GetType().Name} key={key}");

        switch (control)
        {
            case ListBox listBox:
                return InvokeManagedKeyInteraction(rootHandle, listBox, () => NavigateManagedListBox(listBox, key));
            case ComboBox comboBox:
                return InvokeManagedKeyInteraction(rootHandle, comboBox, () => NavigateManagedComboBox(rootHandle, comboBox, key));
            case TreeView treeView:
                return InvokeManagedKeyInteraction(rootHandle, treeView, () => NavigateManagedTreeView(treeView, key));
            case ListView listView:
                return InvokeManagedKeyInteraction(rootHandle, listView, () => NavigateManagedListView(listView, key));
            case DataGridView dataGridView:
                return InvokeManagedKeyInteraction(rootHandle, dataGridView, () => NavigateManagedDataGridView(dataGridView, key));
            case ScrollBar scrollBar:
                return InvokeManagedKeyInteraction(rootHandle, scrollBar, () => NavigateManagedScrollBar(scrollBar, key));
            case CheckBox or RadioButton or IButtonControl when key is Key.Space or Key.Enter:
                return InvokeManagedKeyInteraction(rootHandle, control, () => TryPerformManagedControlClick(rootHandle, target, System.Drawing.Point.Empty));
            default:
                return false;
        }
    }

    private bool InvokeManagedKeyInteraction(HWND rootHandle, Control control, Func<bool> action)
    {
        try
        {
            bool handled = action();
            if (handled)
            {
                MarkDirtyDeferred(rootHandle);
            }

            return handled;
        }
        catch (Exception ex)
        {
            _ = control;
            Application.OnThreadException(ex);
            return true;
        }
    }

    private bool TryHandleManagedComboKeyDown(HWND rootHandle, ImpellerWindowState state, ComboBox comboBox, Key key)
    {
        switch (key)
        {
            case Key.Up:
                SelectManagedComboIndex(comboBox, comboBox.SelectedIndex <= 0 ? 0 : comboBox.SelectedIndex - 1);
                MarkDirty(rootHandle);
                return true;
            case Key.Down:
                SelectManagedComboIndex(comboBox, Math.Min(comboBox.Items.Count - 1, comboBox.SelectedIndex + 1));
                MarkDirty(rootHandle);
                return true;
            case Key.Enter:
            case Key.Escape:
                state.ActiveComboBox = null;
                state.ActiveComboBounds = System.Drawing.Rectangle.Empty;
                MarkDirty(rootHandle);
                return true;
            default:
                return false;
        }
    }

    private bool NavigateManagedComboBox(HWND rootHandle, ComboBox comboBox, Key key)
    {
        switch (key)
        {
            case Key.F4:
            case Key.Space:
            case Key.Enter:
                ShowManagedComboDropDown(rootHandle, comboBox);
                return true;
            case Key.Up:
                SelectManagedComboIndex(comboBox, comboBox.SelectedIndex <= 0 ? 0 : comboBox.SelectedIndex - 1);
                return true;
            case Key.Down:
                SelectManagedComboIndex(comboBox, Math.Min(comboBox.Items.Count - 1, comboBox.SelectedIndex + 1));
                return true;
            case Key.Home:
                SelectManagedComboIndex(comboBox, 0);
                return true;
            case Key.End:
                SelectManagedComboIndex(comboBox, comboBox.Items.Count - 1);
                return true;
            default:
                return false;
        }
    }

    private static void SelectManagedComboIndex(ComboBox comboBox, int index)
    {
        if (comboBox.Items.Count == 0)
        {
            return;
        }

        comboBox.SelectedIndex = Math.Clamp(index, 0, comboBox.Items.Count - 1);
    }

    private static bool NavigateManagedListBox(ListBox listBox, Key key)
    {
        if (listBox.Items.Count == 0)
        {
            return false;
        }

        int visibleItems = Math.Max(1, listBox.Height / Math.Max(1, listBox.ItemHeight));
        int current = listBox.SelectedIndex >= 0 ? listBox.SelectedIndex : Math.Clamp(listBox.TopIndex, 0, listBox.Items.Count - 1);
        int next = key switch
        {
            Key.Up => current - 1,
            Key.Down => current + 1,
            Key.Home => 0,
            Key.End => listBox.Items.Count - 1,
            Key.PageUp => current - visibleItems,
            Key.PageDown => current + visibleItems,
            _ => current,
        };

        if (next == current && key is not (Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
        {
            return false;
        }

        next = Math.Clamp(next, 0, listBox.Items.Count - 1);
        listBox.SelectedIndex = next;
        int maximumTopIndex = Math.Max(0, listBox.Items.Count - visibleItems);
        if (next < listBox.TopIndex)
        {
            listBox.TopIndex = next;
        }
        else if (next >= listBox.TopIndex + visibleItems)
        {
            listBox.TopIndex = Math.Clamp(next - visibleItems + 1, 0, maximumTopIndex);
        }

        return true;
    }

    private static bool NavigateManagedTreeView(TreeView treeView, Key key)
    {
        var rows = new List<(TreeNode Node, int Depth)>();
        foreach (TreeNode node in treeView.Nodes)
        {
            AddVisibleTreeRows(node, 0, rows, int.MaxValue);
        }

        if (rows.Count == 0)
        {
            return false;
        }

        int current = treeView.SelectedNode is { } selectedNode
            ? Math.Max(0, rows.FindIndex(row => ReferenceEquals(row.Node, selectedNode)))
            : 0;
        int visibleRows = Math.Max(1, (treeView.Height - 2) / ManagedTreeItemHeight);
        int next = key switch
        {
            Key.Up => current - 1,
            Key.Down => current + 1,
            Key.Home => 0,
            Key.End => rows.Count - 1,
            Key.PageUp => current - visibleRows,
            Key.PageDown => current + visibleRows,
            _ => current,
        };

        if (key == Key.Left)
        {
            TreeNode node = rows[current].Node;
            if (node.IsExpanded && node.Nodes.Count > 0)
            {
                node.Collapse();
            }
            else if (node.Parent is not null)
            {
                treeView.SelectedNode = node.Parent;
                EnsureManagedTreeNodeVisible(treeView, node.Parent);
            }

            return true;
        }

        if (key == Key.Right)
        {
            TreeNode node = rows[current].Node;
            if (node.Nodes.Count > 0)
            {
                if (!node.IsExpanded)
                {
                    node.Expand();
                }
                else
                {
                    treeView.SelectedNode = node.Nodes[0];
                    EnsureManagedTreeNodeVisible(treeView, node.Nodes[0]);
                }
            }

            return true;
        }

        if (key == Key.Space && treeView.CheckBoxes)
        {
            rows[current].Node.Checked = !rows[current].Node.Checked;
            return true;
        }

        if (next == current && key is not (Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
        {
            return false;
        }

        next = Math.Clamp(next, 0, rows.Count - 1);
        treeView.SelectedNode = rows[next].Node;
        EnsureManagedTreeRowVisible(treeView, next);
        return true;
    }

    private static bool NavigateManagedListView(ListView listView, Key key)
    {
        if (listView.Items.Count == 0)
        {
            return false;
        }

        int visibleRows = Math.Max(1, (listView.Height - ManagedListViewHeaderHeight - 1) / ManagedListItemHeight);
        int current = GetManagedListViewSelectedIndex(listView);
        int next = key switch
        {
            Key.Up => current - 1,
            Key.Down => current + 1,
            Key.Home => 0,
            Key.End => listView.Items.Count - 1,
            Key.PageUp => current - visibleRows,
            Key.PageDown => current + visibleRows,
            _ => current,
        };

        if (next == current && key is not (Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
        {
            return false;
        }

        SelectManagedListViewIndex(listView, Math.Clamp(next, 0, listView.Items.Count - 1));
        return true;
    }

    private static bool NavigateManagedDataGridView(DataGridView dataGridView, Key key)
    {
        int rowCount = GetManagedDataGridViewRowCount(dataGridView);
        if (rowCount == 0)
        {
            return false;
        }

        int visibleRows = Math.Max(1, (dataGridView.Height - ManagedDataGridHeaderHeight - 1) / ManagedDataGridRowHeight);
        int current = GetManagedDataGridViewSelectedRow(dataGridView);
        int next = key switch
        {
            Key.Up => current - 1,
            Key.Down => current + 1,
            Key.Home => 0,
            Key.End => rowCount - 1,
            Key.PageUp => current - visibleRows,
            Key.PageDown => current + visibleRows,
            _ => current,
        };

        if (next == current && key is not (Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
        {
            return false;
        }

        SelectManagedDataGridViewRowIndex(dataGridView, Math.Clamp(next, 0, rowCount - 1));
        return true;
    }

    private static bool NavigateManagedScrollBar(ScrollBar scrollBar, Key key)
    {
        bool vertical = scrollBar is VScrollBar;
        int delta = key switch
        {
            Key.Home => scrollBar.Minimum - scrollBar.Value,
            Key.End => GetScrollBarMaximumValue(scrollBar) - scrollBar.Value,
            Key.PageUp => -scrollBar.LargeChange,
            Key.PageDown => scrollBar.LargeChange,
            Key.Up when vertical => -scrollBar.SmallChange,
            Key.Down when vertical => scrollBar.SmallChange,
            Key.Left when !vertical => -scrollBar.SmallChange,
            Key.Right when !vertical => scrollBar.SmallChange,
            _ => 0,
        };

        if (delta == 0 && key is not (Key.Home or Key.End))
        {
            return false;
        }

        SetManagedScrollBarValue(scrollBar, scrollBar.Value + delta);
        return true;
    }

    private void BeginManagedTextBoxMouseDown(HWND rootHandle, TextBoxBase textBox, System.Drawing.Point clientPoint)
    {
        int index = EstimateTextBoxCaretIndex(textBox, clientPoint);
        if (_windows.TryGetValue(rootHandle, out var state))
        {
            state.ActiveTextSelectionBox = textBox;
            state.TextSelectionAnchor = index;
        }

        textBox.BeginInvoke(new Action(() =>
        {
            using var guard = WinFormsXExecutionGuard.Enter(
                WinFormsXExecutionKind.Input,
                $"TextBoxMouseDown text={textBox.Name}");

            textBox.Select(index, 0);
            MarkDirty(rootHandle);
        }));
    }

    private bool TryUpdateManagedTextBoxSelectionDrag(HWND rootHandle, HWND target, System.Drawing.Point clientPoint)
    {
        if (!_windows.TryGetValue(rootHandle, out var state)
            || state.ActiveTextSelectionBox is not { } textBox
            || (HWND)(nint)textBox.Handle != target)
        {
            return false;
        }

        int caretIndex = EstimateTextBoxCaretIndex(textBox, clientPoint);
        int anchor = Math.Clamp(state.TextSelectionAnchor, 0, textBox.Text.Length);
        textBox.BeginInvoke(new Action(() =>
        {
            int current = Math.Clamp(caretIndex, 0, textBox.Text.Length);
            if (current < anchor)
            {
                textBox.Select(current, anchor - current);
            }
            else
            {
                textBox.Select(anchor, current - anchor);
            }

            MarkDirty(rootHandle);
        }));

        return true;
    }

    private void ReplaceManagedTextSelection(HWND rootHandle, TextBoxBase textBox, string replacement)
    {
        if (textBox.ReadOnly)
        {
            return;
        }

        textBox.SelectedText = replacement;
        MarkDirtyDeferred(rootHandle);
    }

    private static void MoveManagedTextCaret(TextBoxBase textBox, int caretIndex, bool extendSelection)
    {
        caretIndex = Math.Clamp(caretIndex, 0, textBox.Text.Length);
        if (!extendSelection)
        {
            textBox.Select(caretIndex, 0);
            return;
        }

        int selectionStart = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
        int selectionLength = Math.Clamp(textBox.SelectionLength, 0, textBox.Text.Length - selectionStart);
        int anchor = selectionLength == 0 ? selectionStart : selectionStart;
        if (caretIndex < anchor)
        {
            textBox.Select(caretIndex, anchor - caretIndex);
        }
        else
        {
            textBox.Select(anchor, caretIndex - anchor);
        }
    }

    private static int EstimateTextBoxCaretIndex(TextBoxBase textBox, System.Drawing.Point clientPoint)
    {
        float charWidth = EstimateTextBoxCharWidth(textBox);
        int index = (int)MathF.Round((clientPoint.X - ManagedTextPadding) / charWidth);
        return Math.Clamp(index, 0, textBox.Text.Length);
    }

    private static bool IsSilkKeyPressed(IKeyboard keyboard, Key key)
    {
        try
        {
            return keyboard.IsKeyPressed(key);
        }
        catch
        {
            return false;
        }
    }

    private bool TryPerformManagedControlClick(HWND rootHandle, HWND target, System.Drawing.Point clientPoint)
    {
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.Selection,
            $"ManagedClick target=0x{(nint)target:X}");

        Control? control = Control.FromHandle(target);
        if (control is null || !control.Enabled)
        {
            return false;
        }

        switch (control)
        {
            case TextBoxBase:
                return true;
            case CheckBox checkBox:
                InvokeManagedInteraction(rootHandle, control, () =>
                {
                    if (checkBox.ThreeState)
                    {
                        checkBox.CheckState = checkBox.CheckState switch
                        {
                            CheckState.Unchecked => CheckState.Checked,
                            CheckState.Checked => CheckState.Indeterminate,
                            _ => CheckState.Unchecked,
                        };
                    }
                    else
                    {
                        checkBox.Checked = !checkBox.Checked;
                    }

                    TraceInput($"[ManagedCheckBoxClick] text={checkBox.Text} state={checkBox.CheckState}");
                });
                return true;
            case RadioButton radioButton:
                InvokeManagedInteraction(rootHandle, control, () =>
                {
                    radioButton.Checked = true;
                    TraceInput($"[ManagedRadioButtonClick] text={radioButton.Text}");
                });
                return true;
            case IButtonControl buttonControl:
                InvokeManagedInteraction(rootHandle, control, buttonControl.PerformClick);
                return true;
            case ListBox listBox:
                InvokeManagedInteraction(rootHandle, control, () =>
                {
                    int itemHeight = Math.Max(ManagedListItemHeight, listBox.ItemHeight);
                    int topIndex = 0;
                    try
                    {
                        topIndex = Math.Max(0, listBox.TopIndex);
                    }
                    catch
                    {
                    }

                    int index = topIndex + Math.Max(0, clientPoint.Y - 1) / itemHeight;
                    if (index >= 0 && index < listBox.Items.Count)
                    {
                        listBox.SelectedIndex = index;
                        TraceInput($"[ManagedListBoxSelect] index={index} text={listBox.GetItemText(listBox.Items[index])}");
                    }
                });
                return true;
            case ComboBox comboBox:
                ShowManagedComboDropDown(rootHandle, comboBox);
                return true;
            case TreeView treeView:
                InvokeManagedInteraction(rootHandle, control, () => SelectManagedTreeNode(treeView, clientPoint));
                return true;
            case ListView listView:
                InvokeManagedInteraction(rootHandle, control, () => SelectManagedListViewItem(listView, clientPoint));
                return true;
            case DataGridView dataGridView:
                InvokeManagedInteraction(rootHandle, control, () => SelectManagedDataGridViewRow(dataGridView, clientPoint));
                return true;
            case ScrollBar scrollBar:
                InvokeManagedInteraction(rootHandle, control, () => ScrollManagedScrollBarFromClick(scrollBar, clientPoint));
                return true;
            default:
                return false;
        }
    }

    private void InvokeManagedInteraction(HWND rootHandle, Control invoker, Action action)
    {
        invoker.BeginInvoke(new Action(() =>
        {
            using var guard = WinFormsXExecutionGuard.Enter(
                WinFormsXExecutionKind.Selection,
                $"ManagedInteraction {invoker.GetType().Name}");

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Application.OnThreadException(ex);
            }
            finally
            {
                MarkDirtyDeferred(rootHandle);
            }
        }));
    }

    private static void SelectManagedTreeNode(TreeView treeView, System.Drawing.Point clientPoint)
    {
        var rows = new List<(TreeNode Node, int Depth)>();
        foreach (TreeNode node in treeView.Nodes)
        {
            AddVisibleTreeRows(node, 0, rows, int.MaxValue);
        }

        int visibleRows = Math.Max(1, (treeView.Height - 2) / ManagedTreeItemHeight);
        int topRow = Math.Clamp(
            s_managedTreeViewTopRow.TryGetValue(treeView, out int storedTopRow) ? storedTopRow : 0,
            0,
            Math.Max(0, rows.Count - visibleRows));
        int row = topRow + Math.Max(0, clientPoint.Y - 1) / ManagedTreeItemHeight;
        if (row < 0 || row >= rows.Count)
        {
            return;
        }

        var (selectedNode, depth) = rows[row];
        int toggleX = 4 + depth * 18;
        if (selectedNode.Nodes.Count > 0 && clientPoint.X >= toggleX && clientPoint.X <= toggleX + 14)
        {
            if (selectedNode.IsExpanded)
            {
                selectedNode.Collapse();
            }
            else
            {
                selectedNode.Expand();
            }
        }

        treeView.SelectedNode = selectedNode;
        TraceInput($"[ManagedTreeSelect] text={selectedNode.Text}");
    }

    private static void SelectManagedListViewItem(ListView listView, System.Drawing.Point clientPoint)
    {
        int visibleRows = Math.Max(1, (listView.Height - ManagedListViewHeaderHeight - 1) / ManagedListItemHeight);
        int topRow = Math.Clamp(
            s_managedListViewTopRow.TryGetValue(listView, out int storedTopRow) ? storedTopRow : 0,
            0,
            Math.Max(0, listView.Items.Count - visibleRows));
        int row = topRow + (clientPoint.Y - ManagedListViewHeaderHeight - 1) / ManagedListItemHeight;
        if (row < 0 || row >= listView.Items.Count)
        {
            return;
        }

        SelectManagedListViewIndex(listView, row);
    }

    private static void SelectManagedDataGridViewRow(DataGridView dataGridView, System.Drawing.Point clientPoint)
    {
        int visibleRows = Math.Max(1, (dataGridView.Height - ManagedDataGridHeaderHeight - 1) / ManagedDataGridRowHeight);
        int topRow = Math.Clamp(
            s_managedDataGridViewTopRow.TryGetValue(dataGridView, out int storedTopRow) ? storedTopRow : 0,
            0,
            Math.Max(0, GetManagedDataGridViewRowCount(dataGridView) - visibleRows));
        int rowIndex = topRow + (clientPoint.Y - ManagedDataGridHeaderHeight - 1) / ManagedDataGridRowHeight;
        if (rowIndex < 0 || rowIndex >= dataGridView.Rows.Count || dataGridView.Rows[rowIndex].IsNewRow)
        {
            return;
        }

        SelectManagedDataGridViewRowIndex(dataGridView, rowIndex);
    }

    private static void SelectManagedListViewIndex(ListView listView, int row)
    {
        row = Math.Clamp(row, 0, Math.Max(0, listView.Items.Count - 1));
        if (listView.Items.Count == 0)
        {
            return;
        }

        ListViewItem selected = listView.Items[row];
        if (listView.FocusedItem is { } previous && previous != selected)
        {
            previous.Selected = false;
            previous.Focused = false;
        }

        foreach (ListViewItem item in listView.SelectedItems)
        {
            if (item != selected)
            {
                item.Selected = false;
            }
        }

        selected.Selected = true;
        selected.Focused = true;
        listView.FocusedItem = selected;
        s_managedListViewSelection[listView] = row;

        int visibleRows = Math.Max(1, (listView.Height - ManagedListViewHeaderHeight - 1) / ManagedListItemHeight);
        int topRow = s_managedListViewTopRow.TryGetValue(listView, out int storedTopRow) ? storedTopRow : 0;
        if (row < topRow)
        {
            s_managedListViewTopRow[listView] = row;
        }
        else if (row >= topRow + visibleRows)
        {
            s_managedListViewTopRow[listView] = Math.Clamp(row - visibleRows + 1, 0, Math.Max(0, listView.Items.Count - visibleRows));
        }

        InvokeProtectedEvent(listView, "OnSelectedIndexChanged", EventArgs.Empty);
        TraceInput($"[ManagedListViewSelect] index={row} text={selected.Text}");
    }

    private static int GetManagedListViewSelectedIndex(ListView listView)
    {
        if (s_managedListViewSelection.TryGetValue(listView, out int stored)
            && stored >= 0
            && stored < listView.Items.Count)
        {
            return stored;
        }

        if (listView.FocusedItem is { } focused)
        {
            return Math.Max(0, focused.Index);
        }

        return listView.SelectedIndices.Count > 0 ? listView.SelectedIndices[0] : 0;
    }

    private static void SelectManagedDataGridViewRowIndex(DataGridView dataGridView, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= dataGridView.Rows.Count || dataGridView.Rows[rowIndex].IsNewRow)
        {
            return;
        }

        dataGridView.ClearSelection();
        dataGridView.Rows[rowIndex].Selected = true;
        if (dataGridView.Rows[rowIndex].Cells.Count > 0)
        {
            try
            {
                dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[0];
            }
            catch
            {
            }
        }

        s_managedDataGridViewSelection[dataGridView] = rowIndex;

        int visibleRows = Math.Max(1, (dataGridView.Height - ManagedDataGridHeaderHeight - 1) / ManagedDataGridRowHeight);
        int topRow = s_managedDataGridViewTopRow.TryGetValue(dataGridView, out int storedTopRow) ? storedTopRow : 0;
        if (rowIndex < topRow)
        {
            s_managedDataGridViewTopRow[dataGridView] = rowIndex;
        }
        else if (rowIndex >= topRow + visibleRows)
        {
            s_managedDataGridViewTopRow[dataGridView] = Math.Clamp(rowIndex - visibleRows + 1, 0, Math.Max(0, GetManagedDataGridViewRowCount(dataGridView) - visibleRows));
        }

        TraceInput($"[ManagedDataGridViewSelect] row={rowIndex}");
    }

    private static int GetManagedDataGridViewSelectedRow(DataGridView dataGridView)
    {
        if (s_managedDataGridViewSelection.TryGetValue(dataGridView, out int stored)
            && stored >= 0
            && stored < dataGridView.Rows.Count
            && !dataGridView.Rows[stored].IsNewRow)
        {
            return stored;
        }

        if (dataGridView.CurrentRow is { } current && !current.IsNewRow)
        {
            return Math.Max(0, current.Index);
        }

        return dataGridView.SelectedRows.Count > 0 ? dataGridView.SelectedRows[0].Index : 0;
    }

    private static int GetManagedDataGridViewRowCount(DataGridView dataGridView)
    {
        int count = 0;
        foreach (DataGridViewRow row in dataGridView.Rows)
        {
            if (!row.IsNewRow)
            {
                count++;
            }
        }

        return count;
    }

    private static void EnsureManagedTreeNodeVisible(TreeView treeView, TreeNode node)
    {
        var rows = new List<(TreeNode Node, int Depth)>();
        foreach (TreeNode root in treeView.Nodes)
        {
            AddVisibleTreeRows(root, 0, rows, int.MaxValue);
        }

        int rowIndex = rows.FindIndex(row => ReferenceEquals(row.Node, node));
        if (rowIndex >= 0)
        {
            EnsureManagedTreeRowVisible(treeView, rowIndex);
        }
    }

    private static void EnsureManagedTreeRowVisible(TreeView treeView, int rowIndex)
    {
        var rows = new List<(TreeNode Node, int Depth)>();
        foreach (TreeNode root in treeView.Nodes)
        {
            AddVisibleTreeRows(root, 0, rows, int.MaxValue);
        }

        int visibleRows = Math.Max(1, (treeView.Height - 2) / ManagedTreeItemHeight);
        int topRow = s_managedTreeViewTopRow.TryGetValue(treeView, out int storedTopRow) ? storedTopRow : 0;
        if (rowIndex < topRow)
        {
            s_managedTreeViewTopRow[treeView] = rowIndex;
        }
        else if (rowIndex >= topRow + visibleRows)
        {
            s_managedTreeViewTopRow[treeView] = Math.Clamp(rowIndex - visibleRows + 1, 0, Math.Max(0, rows.Count - visibleRows));
        }
    }

    private static void ScrollManagedScrollBarFromClick(ScrollBar scrollBar, System.Drawing.Point clientPoint)
    {
        bool vertical = scrollBar is VScrollBar;
        int coordinate = vertical ? clientPoint.Y : clientPoint.X;
        int length = Math.Max(1, vertical ? scrollBar.Height : scrollBar.Width);
        int arrow = Math.Min(ManagedScrollBarArrowLength, length / 2);
        var thumb = GetScrollBarThumbRectangle(scrollBar);

        if (coordinate < arrow)
        {
            SetManagedScrollBarValue(scrollBar, scrollBar.Value - scrollBar.SmallChange);
        }
        else if (coordinate >= length - arrow)
        {
            SetManagedScrollBarValue(scrollBar, scrollBar.Value + scrollBar.SmallChange);
        }
        else if ((vertical && coordinate < thumb.Top) || (!vertical && coordinate < thumb.Left))
        {
            SetManagedScrollBarValue(scrollBar, scrollBar.Value - scrollBar.LargeChange);
        }
        else if ((vertical && coordinate > thumb.Bottom) || (!vertical && coordinate > thumb.Right))
        {
            SetManagedScrollBarValue(scrollBar, scrollBar.Value + scrollBar.LargeChange);
        }
    }

    private static void SetManagedScrollBarValue(ScrollBar scrollBar, int value)
    {
        int newValue = Math.Clamp(value, scrollBar.Minimum, GetScrollBarMaximumValue(scrollBar));
        if (scrollBar.Value != newValue)
        {
            scrollBar.Value = newValue;
            scrollBar.Invalidate();
        }
    }

    private static int GetScrollBarMaximumValue(ScrollBar scrollBar)
        => Math.Clamp(scrollBar.Maximum - Math.Max(1, scrollBar.LargeChange) + 1, scrollBar.Minimum, scrollBar.Maximum);

    private static void InvokeProtectedEvent(Control control, string methodName, EventArgs args)
    {
        try
        {
            control.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                .Invoke(control, [args]);
        }
        catch (Exception ex)
        {
            Application.OnThreadException(ex);
        }
    }

    private static System.Drawing.Point GetRootRelativeLocation(Control control)
    {
        int x = control.Left;
        int y = control.Top;
        Control? parent = control.Parent;
        while (parent is not null && parent.Parent is not null)
        {
            x += parent.Left;
            y += parent.Top;
            parent = parent.Parent;
        }

        return new System.Drawing.Point(x, y);
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

    private static void TraceInput(string message)
    {
        TracePaint(message);
    }

    private static bool IsManagedClickControl(Control? control)
        => control is IButtonControl
            or CheckBox
            or RadioButton
            or TextBoxBase
            or ListBox
            or ComboBox
            or TreeView
            or ListView
            or DataGridView
            or ScrollBar;

    private static bool IsManagedPushButton(Control? control)
        => control is System.Windows.Forms.Button;

    private static bool IsUserPaintControl(Control control)
        => control.GetType().Assembly != typeof(Control).Assembly
            && control is not Form;

    private static bool RequiresChildClip(Control parent, Control child)
    {
        if (child.Left < 0
            || child.Top < 0
            || child.Right > parent.ClientSize.Width
            || child.Bottom > parent.ClientSize.Height)
        {
            return true;
        }

        return child is TextBoxBase
            or ListBox
            or ComboBox
            or TreeView
            or ListView
            or DataGridView
            or ScrollBar
            or TabControl
            or ScrollableControl { AutoScroll: true };
    }

    private static LPARAM PackMouseLParam(System.Drawing.Point point)
        => (LPARAM)(nint)SafeArithmetic.PackSignedLowHigh16(point.X, point.Y);

    private static int GetMouseWheelDelta(ScrollWheel scrollWheel)
    {
        float y = scrollWheel.Y;
        if (Math.Abs(y) < 0.001f)
        {
            return 0;
        }

        if (Math.Abs(y) >= WheelDelta)
        {
            return Math.Clamp((int)MathF.Round(y), -WheelDelta, WheelDelta);
        }

        return Math.Clamp((int)MathF.Round(y * WheelDelta), -WheelDelta, WheelDelta);
    }

    private bool TryHandleManagedMouseWheel(HWND topLevel, HWND target, int wheelDelta)
    {
        Control? control = Control.FromHandle(target);
        if (control is null)
        {
            return false;
        }

        switch (control)
        {
            case ListBox listBox:
                listBox.BeginInvoke(new Action(() =>
                {
                    int itemHeight = Math.Max(1, listBox.ItemHeight);
                    int visibleItems = Math.Max(1, listBox.Height / itemHeight);
                    int lineDelta = GetManagedWheelLineDelta(wheelDelta, visibleItems);
                    int maximumTopIndex = Math.Max(0, listBox.Items.Count - visibleItems);
                    if (lineDelta != 0 && maximumTopIndex > 0)
                    {
                        listBox.TopIndex = Math.Clamp(listBox.TopIndex + lineDelta, 0, maximumTopIndex);
                        MarkDirty(topLevel);
                    }
                }));
                return true;
            case TreeView treeView:
                treeView.BeginInvoke(new Action(() =>
                {
                    int visibleRows = Math.Max(1, (treeView.Height - 2) / ManagedTreeItemHeight);
                    ScrollManagedTreeView(treeView, GetManagedWheelLineDelta(wheelDelta, visibleRows));
                    MarkDirty(topLevel);
                }));
                return true;
            case ListView listView:
                listView.BeginInvoke(new Action(() =>
                {
                    int visibleRows = Math.Max(1, (listView.Height - ManagedListViewHeaderHeight - 1) / ManagedListItemHeight);
                    ScrollManagedListView(listView, GetManagedWheelLineDelta(wheelDelta, visibleRows));
                    MarkDirty(topLevel);
                }));
                return true;
            case DataGridView dataGridView:
                dataGridView.BeginInvoke(new Action(() =>
                {
                    int visibleRows = Math.Max(1, (dataGridView.Height - ManagedDataGridHeaderHeight - 1) / ManagedDataGridRowHeight);
                    ScrollManagedDataGridView(dataGridView, GetManagedWheelLineDelta(wheelDelta, visibleRows));
                    MarkDirty(topLevel);
                }));
                return true;
            case ScrollBar scrollBar:
                scrollBar.BeginInvoke(new Action(() =>
                {
                    int lineDelta = GetManagedWheelLineDelta(wheelDelta, 1);
                    SetManagedScrollBarValue(scrollBar, scrollBar.Value + lineDelta * Math.Max(1, scrollBar.SmallChange));
                    MarkDirty(topLevel);
                }));
                return true;
            case ScrollableControl scrollableControl when scrollableControl.AutoScroll:
                scrollableControl.BeginInvoke(new Action(() =>
                {
                    int lineDelta = GetManagedWheelLineDelta(wheelDelta, 3);
                    var current = scrollableControl.AutoScrollPosition;
                    scrollableControl.AutoScrollPosition = new System.Drawing.Point(
                        Math.Abs(current.X),
                        Math.Max(0, Math.Abs(current.Y) + lineDelta * ManagedListItemHeight));
                    scrollableControl.Invalidate();
                    MarkDirty(topLevel);
                }));
                return true;
            default:
                return false;
        }
    }

    private static int GetManagedWheelLineDelta(int wheelDelta, int visibleItems)
    {
        int wheelScrollLines = SystemInformation.MouseWheelScrollLines;
        if (wheelScrollLines == 0)
        {
            return 0;
        }

        if (wheelScrollLines == -1)
        {
            wheelScrollLines = Math.Max(1, visibleItems);
        }

        int scrollLines = Math.Max(1, Math.Abs(wheelDelta) * Math.Max(1, wheelScrollLines) / WheelDelta);
        return wheelDelta > 0 ? -scrollLines : scrollLines;
    }

    private static int ScrollManagedTreeView(TreeView treeView, int rowDelta)
    {
        var rows = new List<(TreeNode Node, int Depth)>();
        foreach (TreeNode node in treeView.Nodes)
        {
            AddVisibleTreeRows(node, 0, rows, int.MaxValue);
        }

        int visibleRows = Math.Max(1, (treeView.Height - 2) / ManagedTreeItemHeight);
        int maxTopRow = Math.Max(0, rows.Count - visibleRows);
        int oldTopRow = s_managedTreeViewTopRow.TryGetValue(treeView, out int storedTopRow) ? storedTopRow : 0;
        int newTopRow = Math.Clamp(oldTopRow + rowDelta, 0, maxTopRow);
        s_managedTreeViewTopRow[treeView] = newTopRow;
        treeView.Invalidate();
        return newTopRow;
    }

    private static int ScrollManagedListView(ListView listView, int rowDelta)
    {
        int visibleRows = Math.Max(1, (listView.Height - ManagedListViewHeaderHeight - 1) / ManagedListItemHeight);
        int maxTopRow = Math.Max(0, listView.Items.Count - visibleRows);
        int oldTopRow = s_managedListViewTopRow.TryGetValue(listView, out int storedTopRow) ? storedTopRow : 0;
        int newTopRow = Math.Clamp(oldTopRow + rowDelta, 0, maxTopRow);
        s_managedListViewTopRow[listView] = newTopRow;
        listView.Invalidate();
        return newTopRow;
    }

    private static int ScrollManagedDataGridView(DataGridView dataGridView, int rowDelta)
    {
        int visibleRows = Math.Max(1, (dataGridView.Height - ManagedDataGridHeaderHeight - 1) / ManagedDataGridRowHeight);
        int maxTopRow = Math.Max(0, GetManagedDataGridViewRowCount(dataGridView) - visibleRows);
        int oldTopRow = s_managedDataGridViewTopRow.TryGetValue(dataGridView, out int storedTopRow) ? storedTopRow : 0;
        int newTopRow = Math.Clamp(oldTopRow + rowDelta, 0, maxTopRow);
        s_managedDataGridViewTopRow[dataGridView] = newTopRow;
        dataGridView.Invalidate();
        return newTopRow;
    }

    private static bool TryGetClientPoint(HWND target, System.Drawing.Point rootPoint, out System.Drawing.Point clientPoint)
    {
        Control? control = Control.FromHandle(target);
        if (control is null)
        {
            clientPoint = rootPoint;
            return false;
        }

        var origin = GetRootRelativeLocation(control);
        clientPoint = new System.Drawing.Point(rootPoint.X - origin.X, rootPoint.Y - origin.Y);
        return true;
    }

    private void SetCursorPosFromRootClient(HWND rootHandle, System.Drawing.Point rootClientPoint)
    {
        System.Drawing.Point screenPoint = rootClientPoint;
        if (ClientToScreen(rootHandle, ref screenPoint))
        {
            PlatformApi.Input.SetCursorPos(screenPoint.X, screenPoint.Y);
        }
        else
        {
            PlatformApi.Input.SetCursorPos(rootClientPoint.X, rootClientPoint.Y);
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

    private System.Drawing.Point GetLogicalMousePoint(HWND handle, IMouse mouse)
    {
        float x = mouse.Position.X;
        float y = mouse.Position.Y;

        if (_windows.TryGetValue(handle, out var state))
        {
            int logicalW = state.LastLogicalWidth;
            int logicalH = state.LastLogicalHeight;
            if ((logicalW <= 0 || logicalH <= 0) && state.Width > 0 && state.Height > 0)
            {
                logicalW = state.Width;
                logicalH = state.Height;
            }

            return ClampMousePointToLogical(new System.Drawing.PointF(x, y), logicalW, logicalH);
        }

        return new System.Drawing.Point((int)Math.Floor(x), (int)Math.Floor(y));
    }

    internal static System.Drawing.Point ClampMousePointToLogical(
        System.Drawing.PointF point,
        int logicalW,
        int logicalH)
    {
        float x = point.X;
        float y = point.Y;

        if (logicalW > 0)
        {
            x = Math.Clamp(x, 0, logicalW - 1);
        }

        if (logicalH > 0)
        {
            y = Math.Clamp(y, 0, logicalH - 1);
        }

        return new System.Drawing.Point((int)Math.Floor(x), (int)Math.Floor(y));
    }

    internal static System.Drawing.Point ScaleMousePointToLogical(
        System.Drawing.PointF point,
        int logicalW,
        int logicalH,
        int framebufferW,
        int framebufferH)
    {
        float x = point.X;
        float y = point.Y;
        var (scaleX, scaleY) = ResolveHiDpiScale(logicalW, logicalH, framebufferW, framebufferH);

        if (scaleX > 0f && Math.Abs(scaleX - 1f) >= 0.01f)
        {
            x /= scaleX;
        }

        if (scaleY > 0f && Math.Abs(scaleY - 1f) >= 0.01f)
        {
            y /= scaleY;
        }

        if (logicalW > 0)
        {
            x = Math.Clamp(x, 0, logicalW - 1);
        }

        if (logicalH > 0)
        {
            y = Math.Clamp(y, 0, logicalH - 1);
        }

        return new System.Drawing.Point((int)Math.Floor(x), (int)Math.Floor(y));
    }

    private static string SanitizeTextForImpeller(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var chars = text.ToCharArray();
        bool changed = false;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c == '\u2014' || c == '\u2013')
            {
                chars[i] = '-';
                changed = true;
            }
            else if (c < 32 || c > 126)
            {
                chars[i] = '?';
                changed = true;
            }
        }

        return changed ? new string(chars) : text;
    }

    private static float ParseHiDpiScaleOverride()
    {
        string? raw = Environment.GetEnvironmentVariable("WINFORMSX_HIDPI_SCALE");
        if (!float.TryParse(raw, out float parsed) || parsed <= 0f)
        {
            return OperatingSystem.IsMacOS() ? 2f : 1f;
        }

        return parsed;
    }

    private static long ParseTargetFrameIntervalMs()
    {
        string? raw = Environment.GetEnvironmentVariable("WINFORMSX_TARGET_FPS");
        if (!int.TryParse(raw, out int fps) || fps <= 0)
        {
            fps = DefaultTargetFrameRate;
        }

        fps = Math.Clamp(fps, 1, 240);
        return Math.Max(1, (long)Math.Round(1000.0 / fps));
    }

    private static long ParseMilliseconds(string environmentVariable, int defaultValue, int minValue, int maxValue)
    {
        string? raw = Environment.GetEnvironmentVariable(environmentVariable);
        if (!int.TryParse(raw, out int milliseconds) || milliseconds < 0)
        {
            milliseconds = defaultValue;
        }

        return Math.Clamp(milliseconds, minValue, maxValue);
    }

    private void MarkDirty(HWND hWnd)
        => MarkDirty(hWnd, s_dirtyCoalesceMs);

    private void MarkDirtyImmediate(HWND hWnd)
        => MarkDirty(hWnd, deferMs: 0);

    private void MarkDirtyDeferred(HWND hWnd)
        => MarkDirty(hWnd, s_dirtyCoalesceMs);

    private void MarkDirty(HWND hWnd, long deferMs)
    {
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.Invalidation,
            $"MarkDirty hwnd=0x{(nint)hWnd:X}");

        if (TryGetTopLevelWindowState(hWnd, out var state))
        {
            state.Dirty = true;
            if (deferMs <= 0)
            {
                state.DeferRenderUntilTickMs = 0;
            }
            else if (state.HasPresentedFrame)
            {
                state.DeferRenderUntilTickMs = Environment.TickCount64 + deferMs;
            }
        }
    }

    private bool TryGetTopLevelWindowState(HWND hWnd, out ImpellerWindowState state)
    {
        HWND current = hWnd;
        while (_windows.TryGetValue(current, out state!))
        {
            if (state.Parent == HWND.Null)
            {
                return true;
            }

            current = state.Parent;
        }

        Control? control = Control.FromHandle(hWnd);
        while (control is not null)
        {
            HWND handle = (HWND)(nint)control.Handle;
            if (_windows.TryGetValue(handle, out state!) && state.Parent == HWND.Null)
            {
                return true;
            }

            control = control.Parent;
        }

        foreach (var candidate in _windows.Values)
        {
            if (candidate.Parent == HWND.Null && candidate.Visible)
            {
                state = candidate;
                return true;
            }
        }

        state = null!;
        return false;
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

        try
        {
            Vector2D<int> framebufferPoint = window.PointToFramebuffer(new Vector2D<int>(fallbackWidth, fallbackHeight));
            if (framebufferPoint.X > 0 && framebufferPoint.Y > 0
                && (width == fallbackWidth || height == fallbackHeight
                    || framebufferPoint.X > width || framebufferPoint.Y > height))
            {
                width = framebufferPoint.X;
                height = framebufferPoint.Y;
            }
        }
        catch
        {
        }

        // Some macOS/Vulkan runtime paths report framebuffer == logical even on Retina.
        // Infer the backing framebuffer there; other hosts require explicit HiDPI opt-in.
        bool shouldInferFramebuffer = s_hiDpiMode == HiDpiMode.On
            || (s_hiDpiMode == HiDpiMode.Auto && OperatingSystem.IsMacOS());
        if (shouldInferFramebuffer && s_hiDpiScaleOverride > 1f
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
            state.NativeHwnd = HWND.Null;
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
    public bool UpdateWindow(HWND hWnd)
    {
        MarkDirty(hWnd);
        return true;
    }

    public bool MoveWindow(HWND hWnd, int x, int y, int w, int h, bool repaint)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            if (s.Parent == HWND.Null)
            {
                x = NormalizeTopLevelWindowCoordinate(x, DefaultTopLevelWindowX);
                y = NormalizeTopLevelWindowCoordinate(y, DefaultTopLevelWindowY);
            }
            else
            {
                x = NormalizeChildWindowCoordinate(x);
                y = NormalizeChildWindowCoordinate(y);
            }

            s.X = x;
            s.Y = y;
            s.Width = w;
            s.Height = h;

            if (s.SilkWindow is object)
            {
                s.SilkWindow.Position = new Vector2D<int>(x, y);
                s.SilkWindow.Size = new Vector2D<int>(w, h);
            }

            MarkDirty(hWnd);
            return true;
        }

        return false;
    }

    public unsafe bool SetWindowPos(HWND hWnd, HWND insertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS flags)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOMOVE))
            {
                if (s.Parent == HWND.Null)
                {
                    x = NormalizeTopLevelWindowCoordinate(x, DefaultTopLevelWindowX);
                    y = NormalizeTopLevelWindowCoordinate(y, DefaultTopLevelWindowY);
                }
                else
                {
                    x = NormalizeChildWindowCoordinate(x);
                    y = NormalizeChildWindowCoordinate(y);
                }
            }

            if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOMOVE))
            { s.X = x; s.Y = y; }
            if (!flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOSIZE))
            { s.Width = cx; s.Height = cy; }

            if (s.SilkWindow is object && s.Visible)
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

            MarkDirty(hWnd);
            return true;
        }

        return false;
    }

    public bool GetWindowRect(HWND hWnd, out RECT rect)
    {
        if (_windows.TryGetValue(hWnd, out var s))
        {
            if (TryGetScreenOrigin(hWnd, out System.Drawing.Point origin))
            {
                rect = new RECT(origin.X, origin.Y, origin.X + s.Width, origin.Y + s.Height);
                return true;
            }
        }

        Control? control = Control.FromHandle(hWnd);
        if (control is not null && TryGetControlScreenOrigin(control, out System.Drawing.Point controlOrigin))
        {
            rect = new RECT(controlOrigin.X, controlOrigin.Y, controlOrigin.X + control.Width, controlOrigin.Y + control.Height);
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

        Control? control = Control.FromHandle(hWnd);
        if (control is not null)
        {
            rect = new RECT(0, 0, control.Width, control.Height);
            return true;
        }

        rect = default;
        return false;
    }

    public bool AdjustWindowRectEx(ref RECT rect, WINDOW_STYLE style, bool menu, WINDOW_EX_STYLE exStyle)
        => true; // No chrome adjustment in Impeller

    public int MapWindowPoints(HWND from, HWND to, ref RECT rect)
    {
        System.Drawing.Point topLeft = new(rect.left, rect.top);
        System.Drawing.Point bottomRight = new(rect.right, rect.bottom);
        if (!TryMapWindowPoint(from, to, ref topLeft) || !TryMapWindowPoint(from, to, ref bottomRight))
        {
            return 0;
        }

        rect.left = topLeft.X;
        rect.top = topLeft.Y;
        rect.right = bottomRight.X;
        rect.bottom = bottomRight.Y;
        return 2;
    }

    public int MapWindowPoints(HWND from, HWND to, ref System.Drawing.Point pts, uint count)
    {
        if (count == 0)
        {
            return 0;
        }

        return TryMapWindowPoint(from, to, ref pts) ? 1 : 0;
    }

    public bool ScreenToClient(HWND hWnd, ref System.Drawing.Point pt)
    {
        if (!TryGetScreenOrigin(hWnd, out System.Drawing.Point origin))
        {
            return false;
        }

        pt.X -= origin.X;
        pt.Y -= origin.Y;
        return true;
    }

    public bool ClientToScreen(HWND hWnd, ref System.Drawing.Point pt)
    {
        if (!TryGetScreenOrigin(hWnd, out System.Drawing.Point origin))
        {
            return false;
        }

        pt.X += origin.X;
        pt.Y += origin.Y;
        return true;
    }

    public nint GetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX index)
    {
        if (!_windows.TryGetValue(hWnd, out var s))
            return 0;
        return index switch
        {
            WINDOW_LONG_PTR_INDEX.GWL_STYLE => (nint)unchecked((int)(uint)s.Style),
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE => (nint)unchecked((int)(uint)s.ExStyle),
            WINDOW_LONG_PTR_INDEX.GWL_WNDPROC => s.WndProc,
            WINDOW_LONG_PTR_INDEX.GWL_ID => s.Id,
            WINDOW_LONG_PTR_INDEX.GWL_HWNDPARENT => (nint)s.Parent,
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
            case WINDOW_LONG_PTR_INDEX.GWL_HWNDPARENT:
                s.Parent = (HWND)value;
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

    public HWND WindowFromPoint(System.Drawing.Point pt)
    {
        HWND root = FindTopLevelWindowAtPoint(pt);
        if (root == HWND.Null)
        {
            return HWND.Null;
        }

        return HitTest(root, pt, out _);
    }

    public HWND ChildWindowFromPointEx(HWND parent, System.Drawing.Point pt, uint flags)
    {
        if (!TryGetScreenOrigin(parent, out System.Drawing.Point parentOrigin))
        {
            return HWND.Null;
        }

        System.Drawing.Point screenPoint = new(parentOrigin.X + pt.X, parentOrigin.Y + pt.Y);
        if (!TryGetScreenRect(parent, out System.Drawing.Rectangle parentRect) || !parentRect.Contains(screenPoint))
        {
            return HWND.Null;
        }

        Control? parentControl = Control.FromHandle(parent);
        if (parentControl is null)
        {
            return parent;
        }

        for (int i = 0; i < parentControl.Controls.Count; i++)
        {
            Control child = parentControl.Controls[i];
            if (ShouldSkipChildForPointQuery(child, flags))
            {
                continue;
            }

            if (TryGetControlScreenBounds(child, out System.Drawing.Rectangle childRect) && childRect.Contains(screenPoint))
            {
                return (HWND)(nint)child.Handle;
            }
        }

        return parent;
    }

    public bool EnumChildWindows(HWND parent, Func<HWND, bool> callback) => true;
    public bool EnumWindows(Func<HWND, bool> callback) => true;

    public bool InvalidateRect(HWND hWnd, RECT? rect, bool erase)
    {
        // WinForms invalidates child HWNDs freely. The Impeller render loop owns
        // painting, so invalidation dirties the top-level surface for the next
        // render callback instead of dispatching native paint messages.
        MarkDirty(hWnd);
        return true;
    }

    public bool RedrawWindow(HWND hWnd, RECT? rect, HRGN rgn, REDRAW_WINDOW_FLAGS flags)
    {
        MarkDirty(hWnd);
        return true;
    }

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
        ScreenToClient(root, ref clientPt);
        return HitTestRootClient(root, clientPt, out clientPt);
    }

    private HWND HitTestRootClient(HWND root, System.Drawing.Point rootClientPoint, out System.Drawing.Point clientPt)
    {
        clientPt = rootClientPoint;
        HWND current = root;

        while (true)
        {
            HWND foundChild = HWND.Null;
            var ctrl = Control.FromHandle(current);
            if (ctrl is object)
            {
                if (ctrl is TabControl && clientPt.Y >= 0 && clientPt.Y < ManagedTabHeaderHeight)
                {
                    break;
                }

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

    private bool TryGetControlScreenBounds(Control control, out System.Drawing.Rectangle bounds)
    {
        if (TryGetControlScreenOrigin(control, out System.Drawing.Point topLeft))
        {
            bounds = new System.Drawing.Rectangle(topLeft, control.Size);
            return true;
        }

        bounds = default;
        return false;
    }

    private HWND FindTopLevelWindowAtPoint(System.Drawing.Point pt)
    {
        if (_activeWindow != HWND.Null && TryGetScreenRect(_activeWindow, out System.Drawing.Rectangle activeBounds) && activeBounds.Contains(pt))
        {
            return _activeWindow;
        }

        HWND best = HWND.Null;
        nint bestHandle = 0;
        foreach ((nint handleKey, ImpellerWindowState state) in _windows)
        {
            if (state.Parent != HWND.Null)
            {
                continue;
            }

            HWND handle = (HWND)handleKey;
            if (!TryGetScreenRect(handle, out System.Drawing.Rectangle bounds) || !bounds.Contains(pt))
            {
                continue;
            }

            if (handleKey >= bestHandle)
            {
                best = handle;
                bestHandle = handleKey;
            }
        }

        foreach (Form form in Application.OpenForms)
        {
            if (!form.Visible || !form.IsHandleCreated)
            {
                continue;
            }

            System.Drawing.Rectangle bounds = form.RectangleToScreen(form.ClientRectangle);
            if (bounds.Contains(pt))
            {
                return (HWND)(nint)form.Handle;
            }
        }

        return best;
    }

    private bool TryMapWindowPoint(HWND from, HWND to, ref System.Drawing.Point point)
    {
        if (from != HWND.Null)
        {
            if (!TryGetScreenOrigin(from, out System.Drawing.Point fromOrigin))
            {
                return false;
            }

            point.X += fromOrigin.X;
            point.Y += fromOrigin.Y;
        }

        if (to != HWND.Null)
        {
            if (!TryGetScreenOrigin(to, out System.Drawing.Point toOrigin))
            {
                return false;
            }

            point.X -= toOrigin.X;
            point.Y -= toOrigin.Y;
        }

        return true;
    }

    private bool TryGetScreenRect(HWND hwnd, out System.Drawing.Rectangle rect)
    {
        if (TryGetScreenOrigin(hwnd, out System.Drawing.Point origin) && _windows.TryGetValue(hwnd, out ImpellerWindowState? state))
        {
            rect = new System.Drawing.Rectangle(origin.X, origin.Y, state.Width, state.Height);
            return true;
        }

        Control? control = Control.FromHandle(hwnd);
        if (control is not null && TryGetControlScreenBounds(control, out rect))
        {
            return true;
        }

        rect = default;
        return false;
    }

    private bool TryGetScreenOrigin(HWND hwnd, out System.Drawing.Point origin)
    {
        origin = System.Drawing.Point.Empty;
        if (hwnd == HWND.Null)
        {
            return true;
        }

        if (_windows.TryGetValue(hwnd, out ImpellerWindowState? state))
        {
            long x = state.Parent == HWND.Null
                ? NormalizeTopLevelWindowCoordinate(state.X, DefaultTopLevelWindowX)
                : NormalizeChildWindowCoordinate(state.X);
            long y = state.Parent == HWND.Null
                ? NormalizeTopLevelWindowCoordinate(state.Y, DefaultTopLevelWindowY)
                : NormalizeChildWindowCoordinate(state.Y);
            HWND currentParent = state.Parent;
            while (currentParent != HWND.Null && _windows.TryGetValue(currentParent, out ImpellerWindowState? parentState))
            {
                x += parentState.Parent == HWND.Null
                    ? NormalizeTopLevelWindowCoordinate(parentState.X, DefaultTopLevelWindowX)
                    : NormalizeChildWindowCoordinate(parentState.X);
                y += parentState.Parent == HWND.Null
                    ? NormalizeTopLevelWindowCoordinate(parentState.Y, DefaultTopLevelWindowY)
                    : NormalizeChildWindowCoordinate(parentState.Y);
                currentParent = parentState.Parent;
            }

            origin = new System.Drawing.Point((int)Math.Clamp(x, int.MinValue, int.MaxValue), (int)Math.Clamp(y, int.MinValue, int.MaxValue));
            return true;
        }

        Control? control = Control.FromHandle(hwnd);
        if (control?.IsHandleCreated == true)
        {
            return TryGetControlScreenOrigin(control, out origin);
        }

        return false;
    }

    private bool TryGetControlScreenOrigin(Control control, out System.Drawing.Point origin)
    {
        int x = 0;
        int y = 0;
        Control? current = control;

        while (current is not null)
        {
            if (current.IsHandleCreated)
            {
                HWND handle = (HWND)(nint)current.Handle;
                if (_windows.TryGetValue(handle, out _)
                    && TryGetScreenOrigin(handle, out System.Drawing.Point ancestorOrigin))
                {
                    origin = new System.Drawing.Point(ancestorOrigin.X + x, ancestorOrigin.Y + y);
                    return true;
                }
            }

            x += current.Left;
            y += current.Top;
            current = current.Parent;
        }

        origin = default;
        return false;
    }

    private bool ShouldSkipChildForPointQuery(Control child, uint flags)
    {
        if ((flags & CwpSkipInvisible) != 0 && !child.Visible)
        {
            return true;
        }

        if ((flags & CwpSkipDisabled) != 0 && !child.Enabled)
        {
            return true;
        }

        if ((flags & CwpSkipTransparent) != 0)
        {
            HWND handle = (HWND)(nint)child.Handle;
            if (_windows.TryGetValue(handle, out ImpellerWindowState? state)
                && state.ExStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TRANSPARENT))
            {
                return true;
            }
        }

        return false;
    }

    private void PostMessageToControl(HWND targetWindow, HWND fallbackTopLevel, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var ctrl = Control.FromHandle(targetWindow) ?? Control.FromHandle(fallbackTopLevel) ?? ResolveTopLevelControl(fallbackTopLevel);

        if (ctrl is object)
        {
            HWND dispatchTarget = Control.FromHandle(targetWindow) is null
                ? (HWND)(nint)ctrl.Handle
                : targetWindow;
            bool isMouseMove = msg == WM_MOUSEMOVE;
            nint dispatchHandle = (nint)dispatchTarget;
            nuint keyState = (nuint)wParam;
            bool canCoalesceMouseMove = isMouseMove && (keyState & MkAnyButtonMask) == 0;
            if (canCoalesceMouseMove && !TryQueueMouseMove(dispatchHandle, keyState))
            {
                return;
            }

            void Dispatch()
            {
                try
                {
                    using var guard = WinFormsXExecutionGuard.Enter(
                        WinFormsXExecutionKind.MessageDispatch,
                        $"PostMessageToControl hwnd=0x{(nint)dispatchTarget:X} msg=0x{msg:X}");

                    WPARAM dispatchWParam = wParam;
                    LPARAM dispatchLParam = lParam;

                    if (msg is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN or PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP or PInvoke.WM_CHAR or PInvoke.WM_SYSCHAR)
                    {
                        Control? preprocessTarget = Control.FromChildHandle(dispatchTarget) ?? Control.FromHandle(dispatchTarget);
                        if (preprocessTarget is not null)
                        {
                            Message preprocessMessage = Message.Create(dispatchTarget, (MessageId)msg, dispatchWParam, dispatchLParam);
                            PreProcessControlState preProcessState = Control.PreProcessControlMessageInternal(preprocessTarget, ref preprocessMessage);
                            if (preProcessState == PreProcessControlState.MessageProcessed)
                            {
                                return;
                            }

                            dispatchWParam = preprocessMessage.WParamInternal;
                            dispatchLParam = preprocessMessage.LParamInternal;
                        }
                    }

                    NativeWindow.DispatchMessageDirect(dispatchTarget, msg, dispatchWParam, dispatchLParam, out _);
                }
                finally
                {
                    if (canCoalesceMouseMove)
                    {
                        CompleteMouseMove(dispatchHandle, keyState);
                    }
                }
            }

            ctrl.BeginInvoke(new Action(Dispatch));
        }
    }

    internal bool TryDispatchInputMessage(HWND targetWindow, HWND fallbackTopLevel, uint msg, WPARAM wParam, LPARAM lParam)
    {
        HWND fallback = fallbackTopLevel != HWND.Null
            ? fallbackTopLevel
            : (_activeWindow != HWND.Null ? _activeWindow : targetWindow);

        Control? ctrl = Control.FromHandle(targetWindow) ?? Control.FromHandle(fallback) ?? ResolveTopLevelControl(fallback);
        if (ctrl is null)
        {
            return false;
        }

        PostMessageToControl(targetWindow, fallback, msg, wParam, lParam);
        return true;
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

    private static bool TryQueueMouseMove(nint handle, nuint keyState)
    {
        lock (s_pendingMouseMoveLock)
        {
            return s_pendingMouseMoveTargets.Add((handle, keyState));
        }
    }

    private static void CompleteMouseMove(nint handle, nuint keyState)
    {
        lock (s_pendingMouseMoveLock)
        {
            s_pendingMouseMoveTargets.Remove((handle, keyState));
        }
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
    public bool HasPresentedFrame;
    public int LastLogicalWidth;
    public int LastLogicalHeight;
    public int LastFramebufferWidth;
    public int LastFramebufferHeight;
    public long DeferRenderUntilTickMs;
    public ToolStripMenuItem? ActiveMenuItem;
    public HWND ActiveMenuOwner;
    public System.Drawing.Rectangle ActiveMenuBounds;
    public ComboBox? ActiveComboBox;
    public System.Drawing.Rectangle ActiveComboBounds;
    public HWND MouseDownTarget;
    public Control? PressedControl;
    public TextBoxBase? ActiveTextSelectionBox;
    public int TextSelectionAnchor;
}

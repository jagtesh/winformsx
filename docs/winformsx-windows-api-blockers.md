# WinFormsX Windows API Blocker Inventory

This document combines the controls smoke blockers, the first UIIntegrationTests
blockers, and the Windows API/resource surfaces that still need WinFormsX PAL or
compatibility-facade coverage.

## Current Status Snapshot

- Controls smoke harness: `42 total, 41 passed, 0 failed, 1 skipped`.
- Skipped controls smoke case: `MediaPlayer`, because it is Windows Media Player
  ActiveX and remains intentionally out of scope for the first compatibility
  pass.
- USER32 direct-DllImport facade exists for the first source-compatibility tier:
  cursor/message position, keyboard state, focus/active/foreground window,
  desktop window, system metrics, visibility, enabled state, and the
  `MsgWaitForMultipleObjectsEx` modal-wait path used by PropertyGrid, plus
  `GetWindowPlacement` / `SetWindowPlacement` for MDI minimized-window layout.
- UIIntegrationTests are no longer globally skipped by OS-gated attributes. The
  suite now exposes real WinFormsX behavior gaps. The latest unfiltered broad
  run completes without a hang/abort and reports
  `185 passed, 6 failed, 13 skipped`. Recent passes removed the cross-suite
  `VK_RETURN` stuck-key cascade by making PAL `SendInput` accept packets even
  when a synthetic/stale target cannot be dispatched, then closed focused
  `TabControlTests` by aligning backend tab-rectangle minimum width with Win32
  default tab geometry, and then acknowledged common-control tooltip messages
  through the message PAL so DataGridView tooltip activation no longer depends
  on native tooltip registration. The latest pass also keeps synthetic form
  resize anchored to managed client size, closing focused Button resize
  coverage. Remaining top failures now cluster around broad-suite drag/drop
  state, NumericUpDown accessibility focus, and application handle recreation,
  with the larger provider and dialog/print gaps still tracked below.
- First UIIntegration blockers observed:
  - `OLE32.dll` missing through `Application.ThreadContext.OleRequired()`,
    `InputLanguage.CurrentInputLanguage`, IME, clipboard, and drag/drop paths.
  - `winspool.drv` missing through `DocumentProperties` after the
    `GetDesktopWindow` USER32 gap was closed.
  - Focused `OpenFileDialog`, `FolderBrowserDialog`, and `PrintDialog`
    UIIntegration tests now pass; remaining dialog work is broader managed
    service parity for save/color/font/page setup and native `COMDLG32` facades.
  - `ToolStrip_Hiding_ToolStripMenuItem_OnDropDownClosed_ShouldNotThrow` and
    `ToolStrip_shared_imagelist_should_not_get_disposed_when_toolstrip_does`
    now pass in focused runs.
  - Focused `ListViewTests` coverage is now green with `Passed: 43,
    Failed: 0`; WinFormsX now covers the managed common-control paths for tile
    view info, item/subitem rectangles, hit testing, selected state, next-item
    queries, checkbox double-click, range selection, and basic group keyboard
    navigation.
  - Focused Button UIIntegration coverage is green. Any future button failures
    should be treated as regressions or newly exposed shared infrastructure
    gaps.
  - Focused `PropertyGrid` coverage is now green:
    `38 passed, 0 failed, 0 skipped`. The latest pass closes the
    `MsgWaitForMultipleObjectsEx` modal-loop blocker, selected-entry
    dropdown/dialog button fragment navigation, and PropertyGridView bounds
    reporting.
  - Focused anchor/MDI resize coverage is now green:
    `31 passed, 0 failed`. The latest pass closes the direct
    `GetWindowPlacement` import and MDI minimized-child anchor-bottom failure.
  - Focused `MonthCalendar` coverage is now green:
    `11 passed, 0 failed`. The latest pass routes managed calendar-grid date
    clicks through the same WinFormsX fallback path as navigation clicks, so
    click, double-click, keyboard, and mouse SetDate flows no longer depend on
    native common-control hit testing.
  - Focused drag/drop input coverage is now green except the existing
    explorer-driven upstream skip:
    `DragDropTests` reports `6 passed, 0 failed, 1 skipped`. The latest pass
    keeps hidden/disposed virtual top-level windows out of `WindowFromPoint`
    hit testing and clears stale active-window state during virtual destroy, so
    order-dependent PictureBox and DropImageType failures no longer route input
    through stale form handles from earlier tests.
  - Focused `DataGridViewTests` coverage is now green:
    `3 passed, 0 failed`. The latest pass handles tooltip common-control
    messages in PAL, so `ToolTip.Show` can maintain managed activation state
    without native tooltip-control registration.
  - Focused `ButtonTests` coverage is green again:
    `22 passed, 0 failed`. Synthetic form resize now tracks `ClientSize`
    instead of outer `Form.Size`, so horizontal drags preserve the managed
    display height observed by anchor/layout assertions.
  - Highest-volume remaining failures are accessibility/provider and layout
    clusters: broad-suite drag/drop state, NumericUpDown accessibility focus,
    application handle recreation, ListView tile accessibility, PropertyGrid
    fragment navigation in broad-suite state, RichTextBox link-range behavior,
    dialog/print fallbacks, and remaining lower-volume provider gaps.

## Confirmed Managed Stub Blockers

These are already represented in the fork as WinFormsX compatibility warnings or
safe stubs. They should be treated as known blockers, not successful
implementations.

- Common dialogs:
  - `GetOpenFileName` and `GetSaveFileName` return false.
  - `ChooseColor` returns false.
  - `ChooseFont` returns false.
  - `PrintDlg` and `PageSetupDlg` return false.
- OLE drag/drop:
  - `RegisterDragDrop` returns success without OS registration.
  - `DoDragDrop` now has a managed WinFormsX event-flow implementation. Focused
    UIIntegration coverage is green except the existing explorer-driven skip;
    drag images and richer effect negotiation still need parity work.
  - `RevokeDragDrop` is only useful once registration has real state.
- Win32 dialog lifetime and child lookup:
  - `EndDialog` is acknowledged without closing a native dialog.
  - `GetDlgItem` returns a null handle.
- Menus:
  - `SetMenu`, `GetMenu`, `GetSystemMenu`, and `DrawMenuBar` are partial/no-op
    compatibility points.
- Common controls and theme drawing:
  - `InitCommonControls`, `InitCommonControlsEx`, `DrawFrameControl`,
    `DrawEdge`, and `SetWindowTheme` are acknowledged/no-op style paths.
- IME:
  - `ImmReleaseContext` is a no-op; IME context state is not implemented.
- Shell:
  - `Shell_NotifyIconW` is acknowledged without tray integration.
  - `HtmlHelp` returns a null handle.
- Cursor:
  - `Cursor.GetObjectData` still throws `PlatformNotSupportedException`.
  - Cursor drawing, hot spot, hide/show, and real cursor-image data are minimal.
- Printing:
  - `DefaultPrintController` throws because physical printing has no PAL yet.
- System.Drawing edge cases:
  - Custom line caps cannot wrap native GDI+ handles.
  - Metafile/image-effects paths are partial and should remain tracked as
    drawing-compatibility gaps.

## Blocker Groups And Remedial Plans

### 1. OLE, COM, Clipboard, IME, And Drag/Drop

Impacted APIs and areas:

- `OLE32.dll`: `OleInitialize`, `CoCreateInstance`, `CoGetClassObject`,
  `CoRegisterMessageFilter`, `CreateILockBytesOnHGlobal`,
  `CreateOleAdviseHolder`, `OleCreatePictureIndirect`, `ReleaseStgMedium`,
  storage/stream helpers, and data-object lifetime.
- `OLEAUT32.dll`: `SafeArray*`, `LoadRegTypeLib`, type-library and VARIANT
  support used by COM/property/editor paths.
- `IMM32.dll`: `ImmGetContext`, `ImmReleaseContext`, keyboard layout/input
  language state, and managed IME no-op semantics.
- Clipboard and data transfer: `OleGetClipboard`, `OleSetClipboard`,
  `OleFlushClipboard`, Windows clipboard formats, `DROPFILES`, and file-drop
  data.
- Drag/drop: `RegisterDragDrop`, `RevokeDragDrop`, `DoDragDrop`, drop target
  state, drag enter/over/leave/drop event ordering, drag images, and default
  cursor/effect negotiation.

Plan:

- Add an `OLE32.dll` WinFormsX facade only for ABI-safe
  source-compatibility APIs, backed by PAL state.
- Move core clipboard/data-object/drag-drop behavior into managed PAL services.
- Keep IME v1 as a managed input-language state layer with safe no-op native
  handles.
- Add UIIntegration tests for clipboard, `InputLanguage`, simple drag/drop, and
  file-drop flows before attempting richer COM behavior.

### 2. Dialogs

Impacted APIs and controls:

- `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog`, `ColorDialog`,
  `FontDialog`, `PrintDialog`, `PageSetupDialog`, `MessageBox`, and
  `TaskDialog`.
- Internal WinForms dialogs and modal surfaces: `ThreadExceptionDialog`,
  `GridErrorDialog`, `MdiWindowDialog`, `PrintPreviewDialog`,
  `PrintControllerWithStatusDialog`, PropertyGrid error/editor dialogs, and
  file-dialog overwrite/create prompts.
- Native surfaces: `COMDLG32.dll` (`GetOpenFileName`, `GetSaveFileName`,
  `ChooseColor`, `ChooseFont`, `PrintDlg`, `PrintDlgEx`, `PageSetupDlg`,
  `CommDlgExtendedError`), shell item dialogs, `GetDlgItem`, and `EndDialog`.
- Current UIIntegration evidence: focused open-file, folder-browser, and
  print-dialog tests pass; broader save/color/font/page-setup automation and
  native common-dialog facade coverage remain incomplete.

Plan:

- Build managed WinFormsX modal dialog services for file/save/folder,
  color/font, message box, task dialog, print, and page setup.
- Add test automation hooks so UIIntegration can accept/cancel dialogs without
  native OS dialog windows.
- Keep native common-dialog exports as facades over those services where the ABI
  is simple enough.
- For `TaskDialog`, cover command links, verification checkbox, radio buttons,
  progress state, hyperlinks, page navigation, owner/modal lifetime, and default
  button behavior.

### 3. Printing And Spooler

Impacted APIs and areas:

- `winspool.drv`: `DocumentProperties`, `EnumPrinters`,
  `DeviceCapabilities`, and printer settings enumeration.
- `GDI32.dll` printing: `CreateDC`, `CreateICW`, `StartDoc`, `StartPage`,
  `EndPage`, `EndDoc`, `AbortDoc`, `ExtEscape`.
- WinForms controls: `PrintDialog`, `PageSetupDialog`, `PrintPreviewDialog`,
  `PrintDocument`, and `PrintControllerWithStatusDialog`.

Plan:

- Add a printing PAL with a deterministic "no physical printers" profile and an
  optional file/PDF-like virtual target later.
- Implement `PrinterSettings` and `PageSettings` without requiring `winspool`.
- Facade only the small `winspool.drv` calls needed by source-compatible tests.
- Keep real platform print-provider work separate from the first no-printer pass.

### 4. USER32 Remaining Surface

Already covered by the first facade:

- `GetCursorPos`, `SetCursorPos`, `GetAsyncKeyState`, `MapVirtualKey`,
  `SendInput`, `GetFocus`, `SetFocus`, `GetDesktopWindow`, `GetActiveWindow`,
  `SetActiveWindow`, `GetForegroundWindow`, `SetForegroundWindow`,
  `GetSystemMetrics`, `IsWindow`, `IsWindowVisible`, `IsWindowEnabled`, and
  `EnableWindow`.

Still blocked or incomplete:

- Window/message APIs: `CreateWindowEx`, `DestroyWindow`, `DefWindowProc`,
  `CallWindowProc`, `SendMessage`, `PostMessage`, `PeekMessage`, `GetMessage`,
  `DispatchMessage`, `TranslateMessage`, `RegisterWindowMessage`,
  `UpdateWindow`, `InvalidateRect`, `ValidateRect`, and paint message flow.
- Geometry and hierarchy: `GetWindowRect`, `GetClientRect`, `ClientToScreen`,
  `ScreenToClient`, `MapWindowPoints`, `WindowFromPoint`,
  `ChildWindowFromPointEx`, `GetParent`, `SetParent`, `GetWindow`,
  `GetAncestor`, and `IsChild`.
- Menus: `CreateMenu`, `DestroyMenu`, `GetMenu`, `SetMenu`, `GetSystemMenu`,
  `EnableMenuItem`, `GetMenuItemInfo`, `GetMenuItemCount`, `DrawMenuBar`.
- Input/capture: `SetCapture`, `ReleaseCapture`, `GetKeyState`,
  `GetKeyboardState`, `ActivateKeyboardLayout`, `GetKeyboardLayout`,
  `GetKeyboardLayoutList`, mouse hit testing, double-click timing, and drag
  initiation thresholds.
- Resources: `LoadIcon`, `LoadCursor`, `CopyImage`, `CopyCursor`,
  `DestroyIcon`, `DestroyCursor`, `DrawIcon`, `DrawIconEx`, `GetIconInfo`,
  and `CreateIconFromResourceEx`.
- DPI/system parameters: `SystemParametersInfo`, `SystemParametersInfoForDpi`,
  `GetDpiForWindow`, `GetDpiForSystem`, thread/window DPI awareness context,
  monitor enumeration, high contrast, work area, non-client metrics, caret blink
  time, and power/status probes.
- Cautious tier: hooks, timers, subclass/window-proc callbacks, MDI frame/child
  proc behavior, raw input, global OS state, and APIs where Windows callback
  semantics matter.

Plan:

- Keep PAL as the owner of behavior and extend the native USER32 facade only
  when a source-compatible direct DllImport is likely in production apps.
- Add facade tests in tiers: safe scalar/state APIs first, then message/window
  callback APIs when PAL semantics exist.
- Prefer managed `PInvoke.*` wrappers for WinFormsX internal code; use direct
  native USER32 only to test external source-compatibility.

### 5. KERNEL32 And Process/Resource Infrastructure

Impacted APIs and areas:

- Module/resource loading: `GetModuleHandle`, `LoadLibrary`, `FreeLibrary`,
  `GetProcAddress`, `GetModuleFileName`, activation context APIs, resource
  lookup, and manifest-like behavior.
- Memory/handles: `GlobalAlloc`, `GlobalReAlloc`, `GlobalLock`,
  `GlobalUnlock`, `GlobalSize`, `GlobalFree`, `LocalAlloc`, `LocalFree`,
  `CloseHandle`, `DuplicateHandle`.
- Process/thread state: `GetCurrentProcess`, `GetCurrentProcessId`,
  `GetCurrentThread`, `GetCurrentThreadId`, `GetExitCodeThread`,
  `GetStartupInfo`, and timing/perf counters.
- Locale/error: `FormatMessage`, `GetLocaleInfoEx`, `GetSystemDefaultLCID`,
  `GetThreadLocale`, `GetACP`, `SetLastError`, and last-error propagation.

Plan:

- Implement memory handles and last-error propagation consistently for all native
  compatibility facades.
- Add a small `KERNEL32.dll` facade only after tests identify direct-DllImport
  production surfaces that cannot be handled by managed wrappers.
- Keep process-global and activation-context APIs conservative and deterministic.

### 6. GDI32, GDI+, MSIMG32, And Drawing Handles

Impacted APIs and areas:

- Device contexts and drawing handles: `GetDC`, `ReleaseDC`, `GetDCEx`,
  `CreateCompatibleDC`, `DeleteDC`, `SaveDC`, `RestoreDC`, `SelectObject`,
  `DeleteObject`, `GetObject`, `GetObjectType`, `WindowFromDC`.
- Bitmaps/regions/palettes: `CreateBitmap`, `CreateCompatibleBitmap`,
  `CreateDIBSection`, `GetDIBits`, `CreateRectRgn`, `CombineRgn`,
  `SelectClipRgn`, `GetRegionData`, `GetPaletteEntries`,
  `GetSystemPaletteEntries`, and `CreateHalftonePalette`.
- Text/metrics/colors: `CreateFontIndirect`, `GetTextMetrics`,
  `GetTextExtentPoint32`, `DrawText`, `DrawTextEx`, `ExtTextOut`,
  `GetDeviceCaps`, `SetTextColor`, `SetBkColor`, `GetBkMode`, `SetBkMode`,
  `GetStockObject`, `GetSysColor`, and `GetSysColorBrush`.
- Blitting/theme primitives: `BitBlt`, `AlphaBlend`, `TransparentBlt`,
  `DrawFrameControl`, `DrawEdge`, `DrawTheme*`, and buffered/theme rendering.
- GDI+: `GdiplusStartup`, image codecs, bitmap/icon conversion, paths, pens,
  brushes, matrices, string formatting, image attributes, effects, and
  metafiles.

Plan:

- Continue routing public `System.Drawing` behavior through managed/Impeller
  backends.
- Add native GDI facade coverage only for lightweight handles, metrics, icon
  conversion, and compatibility probes.
- Treat full GDI drawing emulation as out of scope unless a WinForms control or
  UIIntegration test requires a bounded subset.

### 7. COMCTL32, Common Controls, And Image Lists

Impacted APIs and controls:

- `COMCTL32.dll`: `InitCommonControls`, `InitCommonControlsEx`, ImageList
  creation/read/write/draw/replace/destroy, common-control metrics and messages.
- Controls: `TreeView`, `ListView`, `TrackBar`, `TabControl`, `StatusStrip`,
  `ToolStrip`, headers, tooltips, and common-control accessibility.
- Current state: managed image-list state exists and controls smoke passes, but
  some image-list native details remain fake or incomplete.

Plan:

- Keep ListView/TreeView/TrackBar logic managed and PAL-backed.
- Fill in ImageList state for icon size, count, replace, stream read/write,
  `GetImageInfo`, and handle lifetime.
- Add direct COMCTL32 facade exports only for ImageList and init APIs that are
  common in source-compatible WinForms apps.

### 8. SHELL32, SHLWAPI, Stock Icons, Tray, And Shell Resources

Impacted APIs and areas:

- Stock/system icons: `SHGetStockIconInfo`, `ExtractAssociatedIcon`, application
  icon, shield, warning/error/info/question, desktop/computer, and file-type
  icons.
- Shell dialogs/items: `SHCreateShellItem`, `SHCreateItemFromParsingName`,
  folder browser shell items, known folders, pinned places, hidden files, and
  recent-files behavior.
- Tray/help/shell execution: `Shell_NotifyIconW`, `FindExecutable`,
  `ShellExecute`, `HtmlHelp`, file associations, and URL launch.
- Clipboard shell formats: `CF_HDROP`, file-drop lists, shell drag images, and
  shell data-object formats.

Plan:

- Add a `SystemResources` PAL for stock icons, stock cursors, file/folder icons,
  dialog icons, and shell item metadata.
- Prefer existing repo assets first, including `Button.ico`, `ImageInError.ico`,
  `PropertiesTab.ico`, `ShieldIcon.ico`, and test BMP/PNG resources.
- Add only permissively licensed assets when absent: MIT, Apache-2.0, BSD, OFL,
  or CC0-compatible. No runtime downloads.
- Keep tray integration as a later platform-provider feature; v1 can preserve
  API success/state without OS tray display.

### 9. Cursors, Icons, Fonts, And Embedded Resources

Required stock cursor coverage:

- Default arrow, I-beam, wait, app-starting, cross, hand, help, no, size all,
  size NESW, size NWSE, size NS, size WE, up arrow, horizontal/vertical split,
  pan cursors, and custom cursor streams/files.

Required stock icon coverage:

- Application, asterisk/information, error/hand, exclamation/warning, question,
  shield variants, WinLogo compatibility, desktop/computer, folder/file,
  property-grid/editor icons, task-dialog icons, message-box icons, error
  provider icon, and default form icon.

Required resource coverage:

- DataGridView row-header glyphs, DataGridView error icon, PropertyGrid tabs and
  editor buttons, ToolStrip/Menu checkmarks and overflow/dropdown arrows,
  TreeView/ListView sample images, drag/drop sample images, dialog imagery,
  calendar/combo/dropdown arrows, scrollbars, and default disabled-image
  transforms.
- `.resx` load/save and `ResXDataNode` object serialization behavior for
  resources that contain images, icons, cursors, streams, and type metadata.

Required font coverage:

- Default UI font metrics across macOS/Linux.
- Dialog and message-box font fallbacks.
- FontDialog enumeration data.
- Optional embedded fallback font only if host fonts are insufficient; use OFL
  or another accepted permissive license and update notices.

Plan:

- Centralize all stock resource lookup in PAL/resource services.
- Embed assets at build/package time with license notices.
- Add tests that enumerate all stock cursors/icons/resources and construct each
  without touching Windows DLLs.

### 10. RichEdit And Text Editing

Impacted APIs and controls:

- `RichTextBox`, `TextBoxBase`, link detection, text ranges, selection,
  scrolling, undo/redo, RTF load/save, IME/input language interaction, and OLE
  object support.
- Native blocker: `MsftEdit.DLL`/RichEdit DLL loading must not be required.

Plan:

- Keep the managed RichTextBox fallback as the v1 path: plain text, basic RTF
  import/export, selection, multiline paint, links, and smoke-safe construction.
- Consider `RtfPipe` only if RTF parsing becomes large enough to justify a
  permissive dependency.
- Do not adopt a Skia/HarfBuzz-heavy rich text runtime unless Impeller text
  integration requires it later.

### 11. Accessibility And UI Automation

Impacted APIs and areas:

- `UIAutomationCore.dll`: provider creation, events, notification events,
  disconnect, clients-are-listening probes, and raw element provider returns.
- `OLEACC.dll`: legacy accessible object support.
- Controls: ListView, PropertyGrid, ToolStrip/Menu, MonthCalendar, ComboBox,
  DataGridView, RichTextBox, and nested property-grid editor controls.

Plan:

- Keep accessibility objects managed and deterministic for UIIntegration tests.
- Add native facades only for provider/probe functions that tests or source
  apps directly import.
- Fix PropertyGrid and ToolStrip hangs before broadening accessibility coverage,
  because they obscure real failure stacks.

### 12. UXTHEME, DWM, SHCore, Power, And Miscellaneous Windows State

Impacted APIs:

- `UXTHEME.dll`: theme open/close, theme fonts, colors, drawing, app
  properties, documentation properties, and parent background drawing.
- `DWMAPI.dll`: window corner preference and other DWM window attributes.
- `SHCore.dll`: process/window DPI awareness.
- `Powrprof.dll`: power status.
- `Propsys.dll`: property-store helpers.
- `hhctrl.ocx`: HTML Help.
- `ntdll.dll`: any low-level probes that remain in generated surface.

Plan:

- Return deterministic no-theme/no-DWM defaults for v1 unless a control needs a
  visible managed equivalent.
- Route DPI awareness through the WinFormsX system PAL.
- Treat power, property-store, and HTML Help as source-compatible stubs until a
  real app/test needs richer behavior.

### 13. Microsoft.Win32 SystemEvents, Registry, And User Preferences

Impacted APIs and areas:

- `Microsoft.Win32.SystemEvents`: `UserPreferenceChanging`,
  `UserPreferenceChanged`, `DisplaySettingsChanging`, color/theme changes,
  power-mode changes, session changes, and timer events.
- Current WinForms dependencies: `Application`, `Screen`, `InputLanguage`,
  `MonthCalendar`, `DateTimePicker`, `RichTextBox`, `ProgressBar`,
  `UpDownBase`, `PropertyGrid`, `DataGridView`, `ToolStrip`, `ProfessionalColors`,
  `DisplayInformation`, and default system color/font handling.
- `Microsoft.Win32.Registry`: dark-mode preference probes, localized MUI string
  lookup, control setting probes, and source-compatible apps that read the same
  WinForms-adjacent settings.
- Native registry/resource surface: `RegLoadMUIString`, MUI string fallback,
  and default values for missing Windows registry keys.

Plan:

- Keep the public Microsoft.Win32 APIs owned by the runtime where possible, but
  provide WinFormsX PAL events that WinForms controls can subscribe to without a
  hidden Windows broadcast window.
- Add compatibility behavior for the SystemEvents subset production WinForms
  apps commonly rely on: display changes, user-preference changes, color/theme
  changes, and power/session notifications.
- Avoid creating a broad registry emulator. Provide deterministic defaults for
  WinForms-adjacent settings and only facade native registry APIs when tests or
  real apps require them.

## Controls Smoke Regression Watchlist

The controls smoke harness currently passes all non-MediaPlayer cases, but these
cases were previously blockers and should remain regression targets:

- Resource/test assets: `Buttons`, `DataGridView`, `Drag and Drop`, `Dialogs`,
  `PropertyGrid`, `FontNameEditor`, `CollectionEditors`.
- Rich text/text input: `MultipleControls`, `RichTextBoxes`, `TextBoxes`.
- Native common-control paths: `TreeView, ImageList`, `ListView`, `MDI Parent`,
  `TrackBars`.
- Handle/message conversion: `Calendar`, `DateTimePicker`.
- ToolStrip/menu layout: `Menus`, `ToolStrips`,
  `ToolStripSeparatorPreferredSize`.
- Rendering/backend availability: `Splitter`, `ScrollableControlsButton`.
- Modal/ownership diagnostics: `MessageBox`, `DockLayout`, `FormOwnerTest`.

## Recommended Implementation Order

1. Stabilize diagnostics and hang isolation for UIIntegrationTests so every
   failure gives a stack and the suite does not stall on ToolStrip/PropertyGrid.
2. Implement OLE/clipboard/IME basics, because `OLE32.dll` is the first broad
   blocker and unblocks DataGridView, drag/drop, RichTextBox, clipboard, and
   InputLanguage tests.
3. Implement managed dialog services and test automation hooks, covering
   open/save/folder/color/font/message/task/print/page-setup dialogs.
4. Implement printing PAL no-printer behavior and the narrow `winspool.drv`
   facade needed by `PrinterSettings` and `PrintDialog`.
5. Expand USER32 facade coverage for resources, menus, geometry, message-loop,
   and input APIs that are ABI-safe and common in source-compatible apps.
6. Centralize stock resources: cursors, icons, dialog imagery, property-grid
   images, DataGridView glyphs, default UI font fallbacks, and notices.
7. Fill COMCTL32/ImageList and common-control gaps for ListView, TreeView,
   TrackBar, TabControl, ToolStrip, and accessibility.
8. Add Shell/GDI/theme facades only where tests or production-style source
   compatibility requires them.

## Task Board

- [~] OLE/drag-drop baseline: focused `DragDropTests` coverage is green except
  the existing explorer-driven skip; drag-image/effect-negotiation polish
  remains.
- [ ] OLE/clipboard baseline: managed clipboard set/get and format mapping.
- [ ] IME/input-language baseline: `InputLanguage`/IME no-crash managed state.
- [~] Dialog baseline: focused open-file and folder-browser tests are green;
  save/color/font/message/task/page-setup parity still needs coverage.
- [~] Print baseline: focused `PrintDialog` tests are green; no-printer
  `PrinterSettings`, `PageSetupDialog`, and print-controller behavior remain.
- [ ] USER32 tier expansion: geometry/menu/message APIs used by UI tests.
- [ ] KERNEL32 tier expansion: minimal module/resource and last-error semantics.
- [ ] COMCTL32/ImageList tier: image list ops required by ListView/TreeView tests.
- [ ] Shell/resources tier: stock icons/cursors/resource resolver centralization.
- [ ] RichText tier: drag/drop + link/range compatibility follow-ups.
- [ ] Accessibility tier: PropertyGrid/ListView/ToolStrip UIA parity gaps.
- [ ] Theme/DPI tier: deterministic WinFormsX UXTHEME/DWM/SHCore behavior.
- [ ] SystemEvents tier: WinFormsX compatibility layer for common event subset.

### In Progress (current pass)

- [x] Input key-state fidelity for mouse move messages on WinFormsX backend.
- [x] Managed WinFormsX `DoDragDrop` fallback path wired for Control/ToolStripItem.
- [ ] ToolStrip dropdown close/hide lifecycle parity for UIIntegration full-suite progress.
- [ ] Drag/drop target resolution and event ordering parity for remaining UIIntegration tests.

## Acceptance Bar

- Public `System.Windows.Forms` APIs remain source-compatible with Microsoft
  WinForms.
- WinFormsX internal behavior is PAL-owned; native facades forward to PAL rather
  than becoming separate logic.
- WinFormsX tests do not skip simply because the host OS is not Windows.
- No acceptance-run output contains missing `USER32.dll`, `KERNEL32.dll`,
  `COMCTL32.dll`, `COMDLG32.dll`, `OLE32.dll`, `OLEAUT32.dll`, `GDI32.dll`,
  `winspool.drv`, `SHLWAPI.dll`, `SHELL32.dll`, `UXTHEME.dll`, `IMM32.dll`,
  RichEdit DLL, missing resource, missing cursor/icon/font, or GLFW/Vulkan
  initialization errors.

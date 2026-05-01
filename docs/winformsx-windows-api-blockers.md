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
  Internal WinFormsX menu item probes now also route non-generic
  `EnableMenuItem`, `GetMenuItemCount`, and `GetMenuItemInfo` wrappers through
  PAL, so visible catalog startup does not fall back to generated direct
  `USER32.dll` imports while adjusting the form system menu.
- UIIntegrationTests are no longer globally skipped by OS-gated attributes. The
  suite now exposes real WinFormsX behavior gaps. The latest unfiltered broad
  run completes without a hang/abort and reports
  `250 passed, 0 failed, 1 skipped`. Recent passes removed the cross-suite
  `VK_RETURN` stuck-key cascade by making PAL `SendInput` accept packets even
  when a synthetic/stale target cannot be dispatched, then closed focused
  `TabControlTests` by aligning backend tab-rectangle minimum width with Win32
  default tab geometry, and then acknowledged common-control tooltip messages
  through the message PAL so DataGridView tooltip activation no longer depends
  on native tooltip registration. The latest pass also keeps synthetic form
  resize anchored to managed client size, closing focused Button resize
  coverage, and keeps `Application.OpenForms` idempotent across recreate/dispose
  paths so stale forms no longer poison later drag/drop runs. The latest pass
  also routes `UpDownBase` focus to its embedded edit child and supplies a
  managed accessibility child fallback, closing the remaining NumericUpDown
  focus failure in the broad UIIntegration slice. The current Control pass
  removes the remaining OS split from `Control.AdjustWindowRectExForDpi` and
  `Control.SetAcceptDrops`; both now route through the same PAL-backed
  window/DPI and managed OLE registration paths everywhere. The latest
  SystemInformation pass removes that file's remaining OS split as well:
  metrics, work area, high contrast, non-client metrics, menu fonts, caret
  blink, and system-parameter values now use PAL-backed wrappers everywhere,
  with direct `SystemParametersInfoW` overload resolution fixed to avoid the
  generated native import. The latest NativeWindow pass routes class
  registration and module-handle lookup through the WinFormsX PAL everywhere,
  avoids native `GetClassInfo`/`RegisterClass`/`GetStockObject` calls during
  managed window-class setup, and prunes stale unloaded forms from
  `Application.OpenForms` before registering new visible forms. The latest
  input-language pass routes WinFormsX `InputLanguage` keyboard layout
  get/list/activate calls through PAL, keeps registry layout names as an
  optional data source with managed fallback names, and adds
  `GetKeyboardLayoutList` to the USER32 facade. The latest accessibility pass
  removes UIA notification/event Windows guards and lets the accessibility PAL
  listener state decide whether to raise UIA events. The latest OLE data-object
  pass uses the managed native-interface-to-runtime adapter everywhere when
  composing WinForms data objects, so internal data transfer no longer branches
  through COM pointer marshalling for the runtime `IDataObject` view. The
  latest registry/resource pass removes the MUI-string OS guard as well:
  localized registry resource lookup now attempts the wrapped API and falls
  back to stored registry text on the same runtime path. The latest rich-text
  pass makes the managed RichTextBox fallback the single WinFormsX path for
  construction, text length, simple RTF/text stream in/out, coordinate mapping,
  and link selection/click handling, so it no longer attempts to load native
  RichEdit DLLs. The latest form/calendar/MDI pass removes the MonthCalendar
  `SingleMonthSize` OS guard, routes Form icon/menu/window-state behavior
  through the WinFormsX path, falls back to the managed `SystemIcons`
  application icon when the legacy `wfc` resource is unavailable, and routes
  dummy MDI menu create/destroy through `PlatformApi.Control`. PAL window
  placement now seeds state from `WS_MINIMIZE` / `WS_MAXIMIZE`, preserves
  managed min/max state on `SW_SHOW`, and lets MDI minimized-child anchor-bottom
  layout update through managed placement state. The latest DPI/UIA/layout pass
  treats OS-version helpers as WinFormsX compatibility-level probes, routes
  process/thread DPI awareness and UIAutomationCore calls through PAL-backed
  wrappers, emulates the Windows snap-layout keyboard path through managed input
  state, and marks FlowLayoutPanel/ToolStrip as self-sizing default-layout
  controls so autosized smoke forms no longer trip docking assertions. It also
  removes closed PropertyGrid dropdown holders from `Application.OpenForms`,
  preserving broad-suite OpenForms counts without changing the public collection
  surface. The latest private-core system-parameter pass removes the remaining
  host-API split from `PInvokeCore.SystemParametersInfo` and
  `TrySystemParametersInfoForDpi`, so private core metric callers now use the
  deterministic managed compatibility implementation on the same path. The
  latest drawing-resource pass also removes OS guards from Impeller native
  library selection and font-file discovery: native Impeller candidates now
  derive from the runtime identifier, stale wrong-format libraries still fail
  loudly, and font lookup probes known packaged/system locations with missing
  directories ignored. The latest USER32 clipboard pass adds ABI-safe native
  facade coverage for open/close/empty, set/get data handles, format
  availability, and registered format names, all backed by the WinFormsX system
  PAL. UIIntegration now also clears stray forms at test boundaries to keep
  `Application.OpenForms` deterministic across the shared-process suite. Full
  UIIntegration and controls smoke remain stable after this pass. Larger
  provider and dialog/print gaps remain tracked below. The latest IME pass
  moves WinFormsX IME handling behind the input PAL: generated IMM32 imports
  were removed for internal WinForms calls, managed IME context/open/conversion
  state now lives in `ImpellerInputInterop`, and a direct `IMM32.dll`
  compatibility facade resolves to the same PAL state for source-compatible app
  code. The latest common-dialog pass routes internal `COMDLG32` wrappers
  through a WinFormsX common-dialog interop layer and adds a native
  `COMDLG32.dll` compatibility facade for source-compatible direct DllImports;
  the managed WinFormsX path now has visible file, save, folder, color, and
  font dialog baselines. The latest file-picker pass threads `FilterIndex`
  through the dialog PAL and applies wildcard filter patterns when populating
  managed Open/Save file lists. The latest OpenFileDialog pass extends the
  dialog PAL to return multiple selected paths and covers visible filtered
  multi-select selection. The latest FontDialog passes thread `ShowEffects`,
  `ShowColor`, and `Color` into the managed picker and cover Bold/Underline
  effect selection plus color selection through owner-driven modal automation.
  The latest file-prompt pass keeps
  `SaveFileDialog.OverwritePrompt` active on the managed WinFormsX path,
  routes prompt message boxes to the same owner handle, and covers overwrite,
  create, and missing-open-file prompt behavior. The latest ColorDialog pass
  threads custom-color state into the managed picker and covers owner-driven
  selection of a seeded custom color. Direct COMDLG32 exports still return
  deterministic cancel/default state until ABI-safe visible services are wired
  through. The
  latest printing pass removes generated
  `winspool.drv` imports from the first managed print paths, adds deterministic
  no-printer defaults for `EnumPrinters`, `DeviceCapabilities`, and
  `DocumentProperties`, seeds `PrintDlgEx(PD_RETURNDEFAULT)` with a WinFormsX
  virtual printer, and adds a native `winspool.drv` compatibility facade for
  source-compatible direct DllImports. The latest print-controller pass removes
  generated KERNEL32 `Global*` imports from private-core print memory handling,
  keeps explicitly invalid printer names invalid, and lets
  `StandardPrintController` raise a basic one-page print event flow using an
  offscreen WinFormsX graphics surface. The latest page-setup pass adds a
  visible managed `PageSetupDialog` baseline for margins, orientation, minimum
  margin clamping, and owner-driven modal automation. The latest task-dialog
  pass adds a visible managed `TaskDialog` baseline for created/destroyed
  lifetime, standard/custom button clicks, verification checkbox state, radio
  button state, and text/caption updates routed through the existing public
  `TaskDialogPage`/`TaskDialogButton` API. The latest print-preview pass
  records preview pages into managed bitmaps instead of EMF/metafile pages,
  prepares `PrintPreviewDialog` before `Shown`, and lets print-preview continue
  without the optional status-dialog thread when apartment setup is unavailable.
  The latest internal-modal pass makes managed modal `Form.ShowDialog(owner)`
  post a Win32-compatible owner idle notification on `Shown`, so ordinary
  WinForms modal forms can be closed through the same owner-driven automation
  path used by the managed common dialogs. Focused `ThreadExceptionDialog` and
  `GridErrorDialog` owner-close/details-expansion coverage is now green, with
  `MdiWindowDialog` cancel/OK selected-child coverage added as well. The latest
  design-editor pass adds focused `MaskDesignerDialog` and
  `FormatStringDialog` owner-close/OK coverage, and closes the managed
  `LVM_SETCOLUMNW` gap that blocked ListView-backed design-dialog
  initialization. The latest collection/data-grid editor pass adds focused
  `StringCollectionEditor` and `DataGridViewColumnCollectionDialog`
  owner-close/OK coverage, bringing `InternalModalDialogUITests` to
  `14 passed, 0 failed`; no new PAL/runtime gap was exposed by those editor
  paths. The latest add-column pass gives `DataGridViewAddColumnDialog` a
  built-in DataGridView column type fallback when no design-time discovery
  service is available, adds close/add coverage, and brings
  `InternalModalDialogUITests` to `16 passed, 0 failed`. The latest
  collection-editor family pass exercises `CollectionEditor.EditValue` for
  `TreeNodeCollectionEditor`, `ListViewItemCollectionEditor`,
  `ListViewGroupCollectionEditor`, and `ColumnHeaderCollectionEditor`, and
  fixes virtual window DPI awareness so handles return the DPI context captured
  at creation instead of the later ambient thread context. Focused
  `InternalModalDialogUITests` now reports `20 passed, 0 failed`. The latest
  style-editor pass adds runtime/editor-host fallback coverage for
  `ListViewSubItemCollectionEditor`, `TabPageCollectionEditor`, and
  `StyleCollectionEditor` row/column style add/commit flows. `StyleEditorForm`
  now preserves the designer-backed `TableLayoutPanelDesigner` path when
  present, but can insert/delete/fix up row and column state directly when a
  runtime context has no designer service. Focused `InternalModalDialogUITests`
  now reports `24 passed, 0 failed`. The latest ToolStrip editor pass fills
  the missing `ToolStripCollectionEditor` implementation, forwards it through
  `System.Design`, and adds top-level/dropdown item add/commit coverage.
  Focused `InternalModalDialogUITests` now reports `26 passed, 0 failed`.
  The latest ToolStrip designer pass wires the documented dropdown-item
  `Edit Items...` verb to `DropDownItems`, keeping top-level and nested
  ToolStrip collection editing on the same managed editor path. Focused
  `InternalModalDialogUITests` now reports `27 passed, 0 failed`. The latest
  MessageBox/resource pass renders managed MessageBox icons from the shared
  `SystemIcons` fallback set, raises owner `HelpRequested` from the managed
  Help button, applies first-tier right-alignment/RTL options, and makes
  SystemIcons stock/status fallbacks visually distinct without depending on
  native icon resources or font-based drawing during form startup. The latest
  stock-icon pass also routes `SHGetStockIconInfo` through the managed
  `SystemIcons` fallback set, returning deterministic icon indices, synthetic
  resource paths, and small/large icon handles without shell DLL dependence.
  The latest GDI color/metric pass removes generated native imports for
  `CreateCompatibleDC`, `DeleteDC`, `GetDeviceCaps`, `GetSysColor`,
  `GetSysColorBrush`, `GetTextColor`, and `GetBkColor` from the first managed
  wrapper paths, routes them through WinFormsX PAL/manual compatibility state,
  and makes synthetic text/background DC colors return Win32-style
  previous/current values deterministically. The follow-up lightweight GDI
  state pass also removes generated imports for `GetBkMode`, `SetBkMode`,
  `GetObject`, `GetObjectType`, and `GetStockObject`, adds explicit private-core
  stock object metadata, and routes background-mode and stock-object queries
  through deterministic managed state. The latest COMCTL32/ImageList pass
  expands managed image-list state for icon size changes, image count,
  replace/remove bounds, background-color previous/current behavior,
  `GetImageInfo`, stream write/write-ex success, handle cleanup, and synthetic
  bitmap metadata returned through `GetObject(BITMAP)`. Focused primitive tests
  pass and the controls smoke harness remains at
  `42 total, 41 passed, 0 failed, 1 skipped`. The follow-up common-controls
  init pass makes `InitCommonControls` / `InitCommonControlsEx` stateful:
  valid `INITCOMMONCONTROLSEX` requests record supported feature flags,
  malformed struct sizes return failure, and the Impeller control provider
  uses the same wrapper path. The follow-up native COMCTL32 facade pass adds
  packaged `COMCTL32.dll` shim assets for direct source-compatible DllImports,
  exports `InitCommonControls` / `InitCommonControlsEx`, and verifies direct
  import resolution with focused UIIntegration coverage. The latest native
  ImageList facade pass adds first-tier `ImageList_Create`, destroy,
  add/replace/remove, count, icon-size, `GetImageInfo`, background-color, and
  write/write-ex exports with deterministic native shim state. The latest
  KERNEL32 facade pass removes the generated `GetModuleFileName` import from
  the managed path, adds PAL-backed module-file-name state, and packages a
  source-compatible `KERNEL32.dll` facade for `GetCurrentProcess`,
  `GetCurrentThread`, `GetCurrentProcessId`, `GetCurrentThreadId`,
  `GetModuleHandleW/A`, and
  `GetModuleFileNameW/A`. The follow-up last-error pass moves managed
  `GetLastError` / `SetLastError` to the system PAL and forwards direct
  KERNEL32 imports through the same thread-local WinFormsX state. The latest
  KERNEL32 memory pass adds PAL-backed `GlobalAlloc` / `GlobalReAlloc` /
  `GlobalLock` / `GlobalUnlock` / `GlobalSize` / `GlobalFree` and matching
  `Local*` facade exports for source-compatible direct DllImport consumers,
  with deterministic zero-init, resize, size, lock, and free behavior. The
  activation-context pass routes `CreateActCtx`, `ActivateActCtx`,
  `DeactivateActCtx`, and `GetCurrentActCtx` through PAL-owned thread-local
  state and direct `KERNEL32.dll` facade exports. The latest KERNEL32 basic
  process pass removes generated imports for `CloseHandle`, `DuplicateHandle`,
  `FormatMessage`, `GetExitCodeThread`, `GetLocaleInfoEx`, `GetStartupInfo`,
  `GetThreadLocale`, and `GetTickCount`, then forwards the same direct
  `KERNEL32.dll` exports through PAL-owned state. The latest loader pass routes
  managed and direct `LoadLibraryW/A`, `LoadLibraryExW/A`, `FreeLibrary`, and
  `GetProcAddress` through PAL-owned synthetic module handles, preserving safe
  source-compatible loader behavior without native OS loader dependence. The
  latest resource lookup pass adds conservative PAL-backed `FindResourceW/A`,
  `FindResourceExW/A`, `LoadResource`, `LockResource`, `SizeofResource`, and
  `FreeResource` coverage to the managed wrappers and native `KERNEL32.dll`
  facade; unresolved resources now return deterministic failure/default values
  instead of falling through to host KERNEL32 lookup. The
  latest ImageList follow-up keeps `ImageList.GetBitmap` stable when WinFormsX
  synthetic bitmap handles cannot be materialized by GDI+, preserving shared
  `ToolStrip.ImageList` enumeration after form disposal. The latest DWM pass
  removes generated `DwmGetWindowAttribute` / `DwmSetWindowAttribute` imports
  from internal WinForms calls, stores window attributes in managed state, and
  adds a native `DWMAPI.dll` facade for direct source-compatible DllImport
  callers. Dark-mode and form-corner probes no longer depend on host DWMAPI
  resolution. The latest UXTHEME pass adds a native `UXTHEME.dll` facade for
  first-tier no-theme defaults covering theme open/close, theme metrics,
  margins, simple text/background queries, and no-op draw acknowledgements. The
  latest OLEAUT32 pass adds first-tier direct-import coverage for BSTR
  allocation/free/length, `VariantClear`, `PropVariantClear`, conservative
  `LoadRegTypeLib` failure, and safe-array default behavior; dispatch
  accessibility wrappers now tolerate absent registered type libraries and
  return dispatch failure/default results instead of failing type
  initialization. The current OLEAUT32 SafeArray pass adds minimal owned array
  allocation, bounds, access/unaccess, get/put, and destroy behavior for
  source-compatible callers; full VARIANT/type-library marshaling remains
  future work. The latest GDI32 pass packages a native `GDI32.dll` facade
  for source-compatible direct imports and routes DC creation/deletion, device
  caps, object queries, stock/basic object handles, brush/pen creation, text
  and background colors, and background mode through the WinFormsX GDI PAL. The
  latest shell pass packages native `SHELL32.dll` and `SHLWAPI.dll` facades for
  safe first-tier direct imports: tray notifications are acknowledged, shell
  execute reports an accepted launch, folder/PIDL/shell-item APIs fail
  deterministically, file-drop queries return empty state, and SHLWAPI path
  existence/relative/extension helpers provide basic deterministic behavior.
  The follow-up shell resource pass adds first-tier source-compatible direct
  import coverage for `SHGetStockIconInfo`, `ExtractAssociatedIconW/A`,
  `ExtractIconExW/A`, and `SHGetFileInfoW/A` with deterministic icon handles,
  icon indices, and synthetic path/type defaults; real OS-native file
  association integration remains out of scope.
  The follow-up GDI32 breadth pass adds safe bitmap, DIB section, font, and
  region constructors to the direct-import facade. The current GDI32 breadth
  pass adds safe direct-import coverage for drawing copy/fill calls, palette
  selection/query calls, clipping calls, indirect/pattern brushes, and print
  DC/document lifecycle entry points; these remain deterministic compatibility
  defaults rather than real device or printer output. The current USER32
  resource pass adds safe direct-import coverage for icon/cursor load, copy,
  destroy, draw, info, and resource-icon creation calls; this closes resolution
  failures but still leaves real stock cursor/icon image payloads as a resource
  fidelity task. The follow-up GDI+ facade pass adds first-tier
  source-compatible direct-import coverage for `gdiplus.dll`
  `GdiplusStartup`/`GdiplusShutdown` plus deterministic-safe probes
  (`GdipGetImageDecodersSize`, `GdipCreateBitmapFromScan0`), without
  enabling host-native GDI+ drawing behavior.
  The latest broad UIIntegration snapshot is now green at
  `Failed: 0, Passed: 259, Skipped: 1, Total: 260`.
- First UIIntegration blockers observed:
  - `OLE32.dll` missing through `Application.ThreadContext.OleRequired()`,
    clipboard, and drag/drop paths. `InputLanguage.CurrentInputLanguage`,
    USER32 keyboard layout calls, and the first IMM32 IME context calls now use
    PAL-backed state/facades.
  - The first `winspool.drv` blocker is closed for safe no-printer settings:
    `DocumentProperties`, `EnumPrinters`, and `DeviceCapabilities` now route
    through deterministic WinFormsX defaults, with both managed wrappers and
    direct source-compatible DllImports covered.
  - Focused `OpenFileDialog`, `FolderBrowserDialog`, and `PrintDialog`
    UIIntegration tests now pass. The backend `CommonDialog.ShowDialog(owner)`
    path now passes the real owner handle to PAL dialogs, so owner-driven
    accept/cancel automation receives `WM_ENTERIDLE` and no longer wedges the
    modal loop. Focused managed save-file, color, and font dialog coverage now
    passes as well. Direct `COMDLG32.dll` source-compatibility imports now
    resolve through the WinFormsX facade with safe cancel/default behavior.
    `MessageBox` now uses a visible managed modal baseline with standard
    button-result handling and owner-driven automation. `PageSetupDialog` now
    has a visible managed baseline with focused UIIntegration coverage.
    `TaskDialog` now has a visible managed baseline with focused coverage for
    close, button, verification, radio, and progress-bar flows.
    `PrintPreviewDialog` now has a focused visible-dialog baseline backed by
    managed bitmap preview pages instead of unsupported EMF/metafile
    recording. Ordinary managed modal forms now synthesize `WM_ENTERIDLE` for
    their owner on `Shown`; focused
    `ThreadExceptionDialog`, `GridErrorDialog`, `MdiWindowDialog`,
    `MaskDesignerDialog`, and `FormatStringDialog` coverage passes. Remaining
    dialog work is broader managed service parity for larger
    collection/data-grid editors, OS-native picker integration, and richer
    print automation.
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
    A follow-up pass also fixed the state reset where a child created with
    minimized/maximized style could be normalized back to `SW_SHOW` during
    virtual window creation; `CreateWindowEx`, `ShowWindow`, and managed
    `SetWindowPlacement` now share PAL-backed placement state.
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
  - Broad-suite `Application.OpenForms` and drag/drop state is now green. The
    latest pass makes `Application.OpenForms` membership idempotent and removes
    disposed forms during `Form.Dispose`, closing the recreate-handle over-count
    and the downstream order-dependent drag/drop failures in the full run. A
    later NativeWindow/class-registration sweep also prunes stale unloaded forms
    before adding newly visible forms, so the recreate-handle count remains
    stable when the full UIIntegration order exercises unmanaged-class setup
    paths first.
  - Focused `NumericUpDownAccessibleObject_Focused_ReturnsCorrectValueAsync`
    is now green. `UpDownBase.Focus()` routes to the embedded edit child, and
    `UpDownBaseAccessibleObject` can return managed child accessibility objects
    when no native OLEACC parent wrapper exists.
  - Input-language keyboard layout state now uses the PAL as its source of
    truth. Direct USER32 facade coverage includes `GetKeyboardLayout` and
    `GetKeyboardLayoutList`, while managed `InputLanguage` calls no longer need
    generated direct USER32 imports for current/default layout enumeration.
  - UIA event and notification paths now use `PInvoke.UiaClientsAreListening()`
    as the single PAL-backed gate instead of checking the OS first. The current
    Impeller provider still reports no active UIA clients, so this is a pathway
    cleanup rather than full accessibility-provider parity.
  - WinForms data-object composition now uses the managed
    `NativeInterfaceToRuntimeAdapter` path for the runtime `IDataObject` view.
    Focused drag/drop coverage remains green, so managed drag/drop and
    serialized/non-serialized data transfer no longer depend on a separate COM
    pointer marshalling branch.
  - Registry MUI string lookup now uses a single WinFormsX path: it attempts
    the wrapped `RegLoadMUIString` call and falls back to the plain registry
    value when the localized resource path is unavailable.
  - RichTextBox now uses the managed fallback path everywhere in WinFormsX:
    construction no longer attempts native RichEdit DLL loading, text length
    and line/character coordinate queries use managed text state, stream
    in/out uses the existing simple RTF/plain-text fallback, and link
    formatting/click handling reads managed selection and mapping state without
    OS checks.
  - Latest broad UIIntegration active slice is green:
    `250 passed, 0 failed, 1 skipped`.
  - DPI awareness, UIAutomationCore wrapper, snap-layout keyboard, and
    autosized layout smoke paths now run through the same WinFormsX/PAL pathway
    everywhere. `Application.OpenForms` also stays stable after PropertyGrid
    dropdown editor tests because hidden internal dropdown holder forms are
    removed when closed.
  - Highest-volume remaining failures are accessibility/provider and layout
    clusters outside the current active slice: ListView tile accessibility,
    PropertyGrid provider breadth, RichTextBox link-range behavior,
    dialog/print fallbacks, and remaining lower-volume provider gaps.

## Confirmed Managed Stub Blockers

These are already represented in the fork as WinFormsX compatibility warnings or
safe stubs. They should be treated as known blockers, not successful
implementations.

- Common dialogs:
  - `GetOpenFileName` and `GetSaveFileName` route through the WinFormsX
    common-dialog interop layer and currently return deterministic cancel.
  - `ChooseColor` routes through the same layer and currently returns
    deterministic cancel.
  - `ChooseFont` routes through the same layer and currently returns
    deterministic cancel.
  - `PrintDlg`, `PrintDlgEx`, and `PageSetupDlg` route through the same layer
    and currently return deterministic cancel/default result state.
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
  - Basic IME context, open-status, conversion-status, notify, release,
    create, and association calls are PAL-backed and covered through managed
    wrappers plus direct `IMM32.dll` imports. Full composition windows,
    candidate UI, language-specific conversion behavior, and real platform IME
    integration remain future work.
- Shell:
  - `Shell_NotifyIconW` is acknowledged without tray integration.
  - `HtmlHelp` returns a null handle.
- Cursor:
  - `Cursor.GetObjectData` now serializes custom file/stream cursor payloads
    through `CursorData` instead of throwing `PlatformNotSupportedException`.
    Stock and handle-only cursors still need a real resource/image payload
    provider before they can round-trip bytes.
  - Cursor drawing, hot spot, hide/show, and real stock cursor-image data are
    minimal.
- Printing:
  - `StandardPrintController` can now raise a basic print event flow using an
    offscreen WinFormsX graphics surface, but actual physical/file/PDF output
    still has no PAL-backed provider.
  - `PrinterSettings` can now query deterministic no-printer/virtual-printer
    defaults without requiring host spooler APIs, but real printer enumeration,
    printer-specific DEVMODE state, and actual print output remain incomplete.
- System.Drawing edge cases:
  - Custom line caps cannot wrap native GDI+ handles.
  - Metafile/image-effects paths are partial and should remain tracked as
    drawing-compatibility gaps.

## Blocker Groups And Remedial Plans

### 1. OLE, COM, Clipboard, IME, And Drag/Drop

Impacted APIs and areas:

- `OLE32.dll`: `OleInitialize`, `CoCreateInstance`, `CoGetClassObject`,
  `CoRegisterMessageFilter`, `CreateILockBytesOnHGlobal`,
  `CreateStreamOnHGlobal`, `GetHGlobalFromStream`, `CreateOleAdviseHolder`,
  `OleCreatePictureIndirect`, `ReleaseStgMedium`, `OleIsCurrentClipboard`,
  and data-object lifetime.
- `OLEAUT32.dll`: `SafeArray*`, `LoadRegTypeLib`, type-library and VARIANT
  support used by COM/property/editor paths.
- `IMM32.dll`: basic context handles, open/conversion state, notify/release,
  and context association are PAL-backed; keyboard layout/input language state
  is already routed through USER32/PAL.
- Clipboard and data transfer: `OleGetClipboard`, `OleSetClipboard`,
  `OleFlushClipboard`, Windows clipboard formats, `DROPFILES`, and file-drop
  data.
- Drag/drop: `RegisterDragDrop`, `RevokeDragDrop`, `DoDragDrop`, drop target
  state, drag enter/over/leave/drop event ordering, drag images, and default
  cursor/effect negotiation.

Plan:

- Add an `OLE32.dll` WinFormsX facade only for ABI-safe
  source-compatibility APIs, backed by PAL state. The latest pass adds a native
  first-tier facade for `OleInitialize`, `OleUninitialize`, `CoInitialize`,
  `CoInitializeEx`, `CoUninitialize`, `CoCreateInstance`, `CoGetClassObject`,
  `CoRegisterMessageFilter`, `CreateILockBytesOnHGlobal`,
  `CreateStreamOnHGlobal`, `GetHGlobalFromStream`, conservative
  `ReleaseStgMedium`, conservative `OleCreatePictureIndirect`,
  `OleSetClipboard`, `OleGetClipboard`, `OleFlushClipboard`,
  `OleIsCurrentClipboard`,
  `RegisterDragDrop`, `RevokeDragDrop`, and `DoDragDrop`.
- Add an `OLEAUT32.dll` WinFormsX facade only for ABI-safe
  source-compatibility APIs. The latest pass adds BSTR allocation/free/length,
  `VariantClear`, `PropVariantClear`, deterministic missing-type-library
  behavior for `LoadRegTypeLib`, and minimal owned SAFEARRAY storage for
  creation, bounds, access/unaccess, get/put, and destroy. Registered
  type-library backed dispatch and full automation marshaling remain richer
  COM/type-info gaps.
- Move core clipboard/data-object/drag-drop behavior into managed PAL services.
- Keep IME v1 as a managed input-language/context state layer. Expand only
  toward actual composition/candidate behavior when tests require it.
- Add UIIntegration tests for clipboard, `InputLanguage`, simple drag/drop, and
  file-drop flows before attempting richer COM behavior.

### 2. Dialogs

Impacted APIs and controls:

- `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog`, `ColorDialog`,
  `FontDialog`, `PrintDialog`, `PageSetupDialog`, `MessageBox`, and
  `TaskDialog`.
- Internal WinForms dialogs and modal surfaces: `ThreadExceptionDialog`,
  `GridErrorDialog`, `MdiWindowDialog`, `PrintPreviewDialog`,
  `PrintControllerWithStatusDialog`, `MaskDesignerDialog`,
  `FormatStringDialog`, `StringCollectionEditor`,
  `DataGridViewColumnCollectionDialog`, `DataGridViewAddColumnDialog`,
  PropertyGrid error/editor dialogs, and remaining component-specific editor
  dialogs.
- Native surfaces: `COMDLG32.dll` (`GetOpenFileName`, `GetSaveFileName`,
  `ChooseColor`, `ChooseFont`, `PrintDlg`, `PrintDlgEx`, `PageSetupDlg`,
  `CommDlgExtendedError`), shell item dialogs, `GetDlgItem`, and `EndDialog`.
- Current UIIntegration evidence: focused open-file, folder-browser, save-file,
  color, font, message-box, task-dialog, print-dialog, and page-setup tests
  pass. Managed WinFormsX file, save, folder, color, font, message-box,
  task-dialog, and page-setup dialog services now have visible form baselines
  and honor owner accept/cancel or public-API automation; FontDialog now covers
  managed Bold/Italic/Underline/Strikeout effect controls when `ShowEffects`
  is enabled plus managed color selection when `ShowColor` is enabled, and
  file-dialog prompts now have owner-driven automation for
  overwrite acceptance, create acceptance, and missing-open-file cancellation.
  ColorDialog now covers seeded `CustomColors` selection on the managed path.
  MessageBox now covers managed icon rendering and Help button event routing.
  Native common-dialog facade coverage exists for the first
  safe-cancel tier. Ordinary managed modal forms now synthesize `WM_ENTERIDLE`
  for their owner on `Shown`, and
  focused `ThreadExceptionDialog` / `GridErrorDialog` / `MdiWindowDialog` /
  `MaskDesignerDialog` / `FormatStringDialog` / `StringCollectionEditor` /
  `DataGridViewColumnCollectionDialog` / `DataGridViewAddColumnDialog`, plus
  collection editor commit flows for tree nodes, list-view items, list-view
  groups, column headers, list-view subitems, tab pages, and table-layout row
  and column styles, plus ToolStrip top-level and dropdown item collections.
  Managed ListView column updates now acknowledge
  `LVM_SETCOLUMNW`, which keeps ListView-backed design editors from failing
  during resource initialization. `DataGridViewAddColumnDialog` now has a
  built-in column-type fallback for runtime contexts without design-time type
  discovery. OS-native picker integration and broader component-specific
  editor/status modal breadth remain incomplete.

Plan:

- Expand managed WinFormsX modal dialog services for file/save/folder,
  color/font, message box, print, and page setup beyond the current visible
  baseline, and keep expanding task-dialog parity beyond the first visible
  baseline.
- Keep owner/idle automation covered so UIIntegration can accept/cancel dialogs
  without native OS dialog windows.
- Keep ordinary managed modal `Form.ShowDialog(owner)` covered, because many
  internal WinForms dialogs are plain forms rather than `CommonDialog`
  subclasses.
- Continue expanding component-specific editor modal coverage, starting with
  tree-node/style and ToolStrip collection editors, before moving to richer
  service parity.
- Keep native common-dialog exports as facades over those services where the ABI
  is simple enough.
- For `TaskDialog`, cover command links, verification checkbox, radio buttons,
  progress state, hyperlinks, page navigation, owner/modal lifetime, and default
  button behavior. Verification, radio, and progress range/value/state behavior
  have first focused coverage; command links, hyperlinks, and page navigation
  remain.

### 3. Printing And Spooler

Impacted APIs and areas:

- `winspool.drv`: `DocumentProperties`, `EnumPrinters`,
  `DeviceCapabilities`, and printer settings enumeration. The first safe tier
  now has managed and native-facade coverage with deterministic no-printer
  defaults.
- `GDI32.dll` printing: `CreateDC`, `CreateICW`, `StartDoc`, `StartPage`,
  `EndPage`, `EndDoc`, `AbortDoc`, `ExtEscape`.
- WinForms controls: `PrintDialog`, `PageSetupDialog`, `PrintPreviewDialog`,
  `PrintDocument`, and `PrintControllerWithStatusDialog`.

Plan:

- Add a printing PAL with a deterministic "no physical printers" profile and an
  optional file/PDF-like virtual target later.
- Keep extending `PrinterSettings` and `PageSettings` without requiring a host
  spooler. The first deterministic default path is in place; richer physical or
  virtual printer profiles still need a PAL service.
- Keep `PrintDocument` source-compatible for normal event-driven printing even
  before physical output is implemented: the first `StandardPrintController`
  fallback creates an offscreen graphics surface and raises BeginPrint,
  PrintPage, and EndPrint deterministically.
- Keep the direct `GDI32.dll` print-DC facade deterministic: `CreateDCW/A`,
  `CreateICW/A`, `StartDocW/A`, `StartPage`, `EndPage`, `EndDoc`, `AbortDoc`,
  and `ExtEscape` now resolve for source-compatible callers, but they do not
  imply real printer/file/PDF output.
- Keep the `winspool.drv` facade narrow: only expose PAL-backed calls with
  tested deterministic behavior.
- Keep real platform print-provider work separate from the first no-printer pass.

### 4. USER32 Remaining Surface

Already covered by the first facade:

- `GetCursorPos`, `SetCursorPos`, `GetAsyncKeyState`, `MapVirtualKey`,
  `SendInput`, `GetFocus`, `SetFocus`, `GetDesktopWindow`, `GetActiveWindow`,
  `SetActiveWindow`, `GetForegroundWindow`, `SetForegroundWindow`,
  `GetSystemMetrics`, `IsWindow`, `IsWindowVisible`, `IsWindowEnabled`,
  `EnableWindow`, `UpdateWindow`, `InvalidateRect`, and `ValidateRect`.
- Cautious direct-import message queue slice:
  `RegisterWindowMessageW/A`, `PostMessageW/A`, `SendMessageW/A`,
  `PeekMessageW/A`, `GetMessageW/A`, `TranslateMessage`, `DispatchMessageW/A`,
  and `MsgWaitForMultipleObjectsEx` now resolve through the WinFormsX PAL
  message interop instead of requiring a native USER32 queue.

Still blocked or incomplete:

- Window/message APIs: `CreateWindowEx`, `DestroyWindow`, `DefWindowProc`,
  `CallWindowProc`, and native paint message flow parity.
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
  and `CreateIconFromResourceEx`. The current direct USER32 facade resolves
  these with deterministic handles and no-op-safe draw/info behavior; real
  resource-image payloads and hot spots remain incomplete.
- DPI/system parameters: `SystemParametersInfo`, `SystemParametersInfoForDpi`,
  `GetDpiForWindow`, `GetDpiForSystem`, thread/window DPI awareness context,
  monitor enumeration, high contrast, work area, non-client metrics, caret blink
  time, and power/status probes.
- Cautious tier: hooks, timers, subclass/window-proc callbacks, MDI frame/child
  proc behavior, raw input, global OS state, and APIs where Windows callback
  semantics matter. The newly covered direct-import message APIs are intentionally
  conservative: they route to the managed PAL queue and deterministic dispatch,
  not a host OS USER32 queue, so full Win32 queue filtering and callback timing
  fidelity are still cautious-tier gaps.

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
  `GetProcAddress`, `GetModuleFileName`, first-tier activation context APIs,
  resource lookup, and manifest-like behavior.
- Memory/handles: `GlobalAlloc`, `GlobalReAlloc`, `GlobalLock`,
  `GlobalUnlock`, `GlobalSize`, `GlobalFree`, `LocalAlloc`, `LocalFree`,
  `CloseHandle`, `DuplicateHandle`.
- Process/thread state: `GetCurrentProcess`, `GetCurrentProcessId`,
  `GetCurrentThread`, `GetCurrentThreadId`, `GetExitCodeThread`,
  `GetStartupInfo`, and timing/perf counters.
- Locale/error: `FormatMessage`, `GetLocaleInfoEx`, `GetSystemDefaultLCID`,
  `GetThreadLocale`, `GetACP`, `SetLastError`, and last-error propagation.

Plan:

- Keep resource lookup deterministic, with PAL owning any shared state that
  native compatibility facades expose. First-tier `Global*` and `Local*` memory
  handle behavior is now PAL-backed and covered by direct KERNEL32 facade tests.
- Keep activation-context support conservative: WinFormsX records synthetic
  activation context handles and thread-local activation cookies, without
  pretending to parse or apply Windows manifests yet.
- Keep basic thread/locale/startup/format-message helpers deterministic and
  PAL-owned; they exist to preserve source compatibility for WinForms code paths
  such as startup visibility, COM property descriptors, locale-sensitive page
  setup, and send-key timestamps.
- Keep loader support first-tier and source-compatible: WinFormsX can return
  stable module handles and safe frees for managed/direct callers, but
  `GetProcAddress` remains conservative until PAL can expose real export tables.
- Keep the first-tier `KERNEL32.dll` facade limited to ABI-simple process,
  thread, and module-path APIs until tests justify broader loader/resource
  semantics.
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
- First-tier color and device-cap probes now have PAL/manual wrappers:
  `GetDeviceCaps` returns deterministic display defaults for DPI and color
  depth, `GetSysColor`/`GetSysColorBrush` use the managed system palette, and
  synthetic DC text/background colors preserve per-DC state.
- First-tier stock-object and background-mode probes now avoid generated GDI32
  imports as well: stock brush/object type metadata is deterministic and
  `GetBkMode`/`SetBkMode` preserve per-DC state.
- First-tier direct `gdiplus.dll` facade coverage now resolves
  `GdiplusStartup`/`GdiplusShutdown` and deterministic-safe probe defaults for
  `GdipGetImageDecodersSize` and `GdipCreateBitmapFromScan0` so direct
  DllImport callers do not fail module resolution.
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
- Current state: managed image-list state exists and controls smoke passes.
  First-tier `GetImageInfo`, icon-size mutation, background color, write/write-ex,
  remove/replace bounds, handle cleanup, and synthetic bitmap metadata are now
  covered. Some image-list native details remain fake or incomplete.
  `InitCommonControls` / `InitCommonControlsEx` now preserve deterministic
  requested-class state for managed feature probes, and direct
  source-compatible `COMCTL32.dll` imports for those init APIs now resolve
  through a packaged facade. Direct source-compatible ImageList imports now
  resolve for first-tier stateful create/count/size/info/write operations.

Plan:

- Keep ListView/TreeView/TrackBar logic managed and PAL-backed.
- Continue filling ImageList state beyond the first tier: image payload storage,
  draw composition, mask/overlay semantics, stream read/write payload fidelity,
  and broader native facade parity.
- Add direct COMCTL32 facade exports only for ImageList and init APIs that are
  common in source-compatible WinForms apps; init and first-tier ImageList
  exports are covered.

### 8. SHELL32, SHLWAPI, Stock Icons, Tray, And Shell Resources

Impacted APIs and areas:

- Stock/system icons: first-tier managed/direct-import coverage is in place for
  `SHGetStockIconInfo`, `ExtractAssociatedIconW/A`, `ExtractIconExW/A`, and
  `SHGetFileInfoW/A` with deterministic handles, indices, and synthetic
  metadata defaults. Remaining work covers richer application/file-type icon
  fidelity, shield/warning/info/question variants, and centralized stock icon
  resource mapping beyond first-tier placeholders.
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
- Current asset inventory confirms those required icons and the bundled
  WinForms cursor set already live under repo resources, are embedded by the
  existing project globs, and are covered by `EmbeddedResourceTests` /
  `SystemIconsTests`; no downloaded third-party assets or new notices are
  required for the first pass.
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
- Continue cursor serialization/resource work by routing stock cursor payloads
  through the same central provider instead of leaving known cursors as
  metadata-only synthetic handles.
- `ResXDataNode.ISerializable` now snapshots stable node metadata and file-ref
  state instead of throwing, but full resource payload fidelity still depends
  on the icon/cursor/image providers above.

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

- `UXTHEME.dll`: first-tier native facade coverage for theme open/close, app
  properties, part-size/margin/text metrics, simple string/color/bool queries,
  and no-op-safe draw/background acknowledgements; richer visual-style drawing
  remains managed/PAL-owned.
- `DWMAPI.dll`: first-tier window attribute get/set coverage for corner
  preference, dark-mode, caption-color, and similar stored values; broader
  composition behavior remains out of scope until a real control/test needs it.
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
  changes, and power/session notifications. The latest pass bridges
  `PowerModeChanged`, `SessionEnding`, `SessionEnded`, and `SessionSwitch`
  through both public `SystemEvents` and internal `PalEvents`, with deterministic
  managed raisers and message mapping for power/session broadcasts.
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
   open/save/folder/color/font/message/print/page-setup/task first, then
   internal modal surfaces.
4. Implement the remaining printing PAL behavior above the first no-printer
   defaults: visible print/page-setup services, print-preview integration, and
   print-controller output.
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
- [~] OLE/clipboard baseline: managed clipboard set/get and format mapping.
- [x] IME/input-language baseline: `InputLanguage` and first-tier IME context
  calls use PAL-backed managed state plus USER32/IMM32 facades.
- [~] Dialog baseline: focused open-file and folder-browser tests are green;
  managed file/save/folder/color/font dialogs now have visible WinFormsX
  baselines and focused owner-driven accept/cancel automation, including
  FontDialog effect/color selection, ColorDialog custom-color selection, and
  file-dialog prompt handling; `MessageBox` now has a visible managed modal
  baseline; first-tier `COMDLG32.dll`
  safe-cancel facade is covered; `PageSetupDialog` now has a visible managed
  baseline; `TaskDialog` now has a visible managed baseline; ordinary managed
  modal forms now notify their owner on idle and focused `ThreadExceptionDialog`
  / `GridErrorDialog` / `MdiWindowDialog` / `MaskDesignerDialog` /
  `FormatStringDialog` coverage is green. OS-native picker integration and
  larger internal editor/status modal breadth still need coverage.
- [~] Print baseline: focused `PrintDialog` tests are green; no-printer
  `PrinterSettings`, direct `winspool.drv` defaults, private-core print
  `Global*` memory, invalid-printer validation, and basic
  `StandardPrintController` event flow are covered. `PageSetupDialog` has a
  visible managed baseline, and `PrintPreviewDialog` now renders preview pages
  through managed bitmap-backed `PreviewPrintController` output. Real status
  dialog UI and real print/file/PDF output remain.
- [ ] USER32 tier expansion: geometry/menu/message APIs used by UI tests.
- [~] KERNEL32 tier expansion: process/thread/module-path facade is covered;
  direct last-error and first-tier `Global*` / `Local*` memory state are
  covered; first-tier activation context state and basic thread/locale/startup
  helpers are covered; first-tier timing/perf + locale-codepage probes
  (`QueryPerformanceCounter`, `QueryPerformanceFrequency`, `GetTickCount64`,
  `GetACP`, `GetOEMCP`, `GetSystemDefaultLCID`, `GetUserDefaultLCID`) are
  covered; first-tier loader handles are covered; module resources and richer
  export/resource lookup remain.
- [~] COMCTL32/ImageList tier: first-tier image-list state, synthetic bitmap
  metadata, and managed enumeration fallback are covered; richer draw/mask and
  stream payload fidelity remain.
- [~] Shell/resources tier: stock icons/cursors/resource resolver centralization.
- [ ] RichText tier: drag/drop + link/range compatibility follow-ups.
- [ ] Accessibility tier: PropertyGrid/ListView/ToolStrip UIA parity gaps.
- [ ] Theme/DPI tier: deterministic WinFormsX UXTHEME/DWM/SHCore behavior.
- [ ] SystemEvents tier: WinFormsX compatibility layer for common event subset.

### In Progress (current pass)

- [x] KERNEL32 `Global*` / `Local*` direct facade coverage now routes through
  PAL memory state and passes focused UIIntegration regression coverage.
- [x] KERNEL32 activation-context basics now route through PAL and direct
  facade exports for `CreateActCtx`, `ActivateActCtx`, `DeactivateActCtx`, and
  `GetCurrentActCtx`.
- [x] KERNEL32 basic thread/locale/startup helpers now route through PAL and
  direct facade exports for `CloseHandle`, `DuplicateHandle`, `FormatMessage`,
  `GetExitCodeThread`, `GetLocaleInfoEx`, `GetStartupInfo`, `GetThreadLocale`,
  `GetTickCount`, `GetTickCount64`, `QueryPerformanceCounter`,
  `QueryPerformanceFrequency`, `GetACP`, `GetOEMCP`,
  `GetSystemDefaultLCID`, and `GetUserDefaultLCID`.
- [x] KERNEL32 first-tier loader facade now routes `LoadLibraryW/A`,
  `LoadLibraryExW/A`, `FreeLibrary`, and `GetProcAddress` through PAL-owned
  synthetic module handles.
- [x] USER32 menu item generated-import cleanup now routes internal
  `EnableMenuItem`, `GetMenuItemCount`, and `GetMenuItemInfo` calls through
  PAL-backed wrappers; the visible controls catalog starts and paints without a
  direct `USER32.dll` load failure.
- [x] KERNEL32 resource lookup facade now routes `FindResourceW/A`,
  `FindResourceExW/A`, `LoadResource`, `LockResource`, `SizeofResource`, and
  `FreeResource` through PAL-owned deterministic failure/default behavior.
- [ ] Next KERNEL32 breadth: richer export-table compatibility.
- [x] Managed DWM attribute wrappers now route `DwmSetWindowAttribute` /
  `DwmGetWindowAttribute` through deterministic WinFormsX state for internal
  Form/Control dark-mode, corner, and caption-color probes.
- [x] Native `DWMAPI.dll` facade now resolves direct source-compatible
  `DwmSetWindowAttribute` / `DwmGetWindowAttribute` imports through the same
  deterministic WinFormsX-style attribute state.
- [x] Native `UXTHEME.dll` facade now resolves first-tier direct
  source-compatible theme imports with deterministic no-theme metrics and
  no-op draw/query behavior.

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

# WinFormsX API Remediation Task Board

Source: [winformsx-windows-api-blockers.md](docs/winformsx-windows-api-blockers.md)

Current baseline: `42 total, 41 passed, 0 failed, 1 skipped` with `MediaPlayer` skipped.

## Latest Progress (2026-04-29)

- Landed in `b888d27ff`:
  - Managed non-Windows drag/drop fallback path added and wired for `Control.DoDragDrop` and `ToolStripItem.DoDragDrop`.
  - WinFormsX input backend now propagates mouse key-state flags in message `wParam` for move/down/up paths.
  - `WM_MOUSEMOVE` now uses message key-state on backend-active path so drag logic sees held mouse buttons correctly.
- In-progress local changes (next commit):
  - Non-Windows `DataObject` construction no longer routes through `GlobalInterfaceTable`/`OLE32.dll`; added managed adapter path to avoid `DllNotFoundException`.
  - `BackCompatibleStringComparer` hash code now uses `unchecked` arithmetic to avoid overflow exceptions during drag/drop data storage.
  - Drag/drop enter/over sequencing and input-state synchronization were tightened for backend-driven mouse/key events.
- In-progress local changes (next commit):
  - Implemented `WindowFromPoint`, `ChildWindowFromPointEx`, `ScreenToClient`, `ClientToScreen`, and `MapWindowPoints` in `ImpellerWindowInterop`.
  - Wired mouse message posting in `ImpellerInputInterop` to convert cursor screen coordinates to target-client coordinates before posting.
  - Mouse target selection now prefers hit-testing via `WindowFromPoint` when capture is not active.
  - Added top-level form fallback in `WindowFromPoint` resolution (`Application.OpenForms`), but focused `DragDropTests` still remain at the same 4 failing cases.
  - Added focused/active-root fallback in `ImpellerInputInterop.GetMouseTarget` via `ChildWindowFromPointEx`; focused `DragDropTests` remain unchanged at 4 failures.
- In-progress local changes (next commit):
  - Fixed managed `PInvoke.MapWindowPoints(HWND, HWND, ref Point)` wrapper to call the PAL point/count overload directly (returns `1` for one point, matching USER32/native shim path).
  - Replaced managed `PInvoke.WindowFromPoint` TODO/null stub with PAL-backed implementation (`PlatformApi.Window.WindowFromPoint`).
  - Updated USER32 compatibility integration assertion flow for explicit managed-vs-native parity checks on `WindowFromPoint` and `ChildWindowFromPointEx`.
  - Focused USER32 facade regression now passes:
    - `User32CompatibilityFacadeTests.DirectDllImports_RouteToWinFormsXPal`
- Current focused rerun (`DragDropTests`) has 4 failing cases (was 6):
  - `DragDrop_QueryDefaultCursors_Async`
  - `DragEnter_Set_DropImageType_Message_MessageReplacementToken_ReturnsExpected_Async`
  - `PictureBox_SetData_DoDragDrop_RichTextBox_ReturnsExpected_Async`
  - `ToolStripItem_SetData_DoDragDrop_RichTextBox_ReturnsExpected_Async`

## Task Legend

- `[ ]` Not started
- `[~]` In progress
- `[x]` Done
- Priority: P0 = unblocks core controls/UI tests, P1 = common API, P2 = compatibility surface, P3 = enhancement.

## P0 Unblockers

- [x] WXA-1001: Implement `Microsoft.Win32.SystemEvents` compatibility event bridge for `UserPreference*`, `DisplaySettings*`, and basic power/session timers in `SystemEvents`/`PalEvents` pathways.
- [x] WXA-1002: Expand USER32 shim dispatch table for geometry and hierarchy calls (`SetCapture`, `ReleaseCapture`, `GetKeyState`, `GetKeyboardState`, `GetKeyboardLayout`, `ActivateKeyboardLayout`, `GetWindowRect`, `GetClientRect`, `MapWindowPoints`, `ClientToScreen`, `ScreenToClient`, `GetParent`, `SetParent`, `GetWindow`, `GetAncestor`, `IsChild`, `WindowFromPoint`, `ChildWindowFromPointEx`, `UpdateWindow`, `InvalidateRect`, `ValidateRect`, plus menu entrypoints: `SetMenu`, `GetMenu`, `GetSystemMenu`, `EnableMenuItem`, `GetMenuItemCount`, `GetMenuItemInfo`, `DrawMenuBar`).
- [x] WXA-1003: Add direct-DllImport conformance tests for newly shimmed USER32 geometry/hierarchy/input/menu entrypoints.

## KERNEL32 Surface

- [ ] WXA-1100: Add KERNEL32 compatibility for process/thread/module/memory primitives used by WinForms (`GetModuleHandle`, `LoadLibrary`, `FreeLibrary`, `GetProcAddress`, `GetCurrentProcessId`, `GetCurrentThreadId`, `GetCurrentThread`, `Global*/Local*`, `GetLastError`, `SetLastError`).

## OLE, COM, Clipboard, IME, Drag/Drop

- [ ] WXA-1101: Implement PAL-backed `OleInitialize`, `CoInitialize`, `CoCreateInstance` and core `OLE32.dll` facade contracts.
- [ ] WXA-1102: Implement clipboard helpers (`OleGetClipboard`, `OleSetClipboard`, `OleFlushClipboard`) with managed storage and format metadata.
- [~] WXA-1103: Implement `RevokeDragDrop`/`RegisterDragDrop`/`DoDragDrop` event flow and default drop effects.
- [ ] WXA-1104: Implement `OleInitialize` + `InputLanguage.CurrentInputLanguage` to unblock data-grid and IME-dependent paths.

## Dialog and Common Controls

- [ ] WXA-1201: Implement managed fallbacks for `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog`, `ColorDialog`, `FontDialog`.
- [ ] WXA-1202: Implement managed `PrintDialog` and `PageSetupDialog` with no-spooler fallback path.
- [ ] WXA-1203: Implement non-Windows fallback for internal modal dialogs (`PrintPreviewDialog`, `TaskDialog`, `GridErrorDialog`, `ThreadExceptionDialog`).
- [ ] WXA-1204: Route native `COMDLG32.dll` symbols used by `PInvoke` (`GetOpenFileName`, `GetSaveFileName`, `ChooseColor`, `ChooseFont`, `PrintDlg`, `PrintDlgEx`, `PageSetupDlg`) to WinFormsX-managed dialog services.

## Printing And Spooler

- [ ] WXA-1301: Implement printer settings service without hard OS printer dependency (`PrinterSettings`, `PageSettings`).
- [ ] WXA-1302: Add minimal safe `winspool.drv` facade (`DocumentProperties`, `EnumPrinters`, `DeviceCapabilities`) with deterministic defaults.
- [ ] WXA-1303: Implement fallback graphics path for `PrintDocument` and `PrintControllerWithStatusDialog` for integration tests.

## USER32 Surface (Tiered)

- [x] WXA-1401 (tier-1): Implement window/message-safe stubs for input and focus (`GetKeyState`, `GetKeyboardState`, `ActivateKeyboardLayout`, `GetKeyboardLayout`, layout queries).
- [x] WXA-1402 (tier-1): Implement menu APIs (`SetMenu`, `GetMenu`, `GetSystemMenu`, `EnableMenuItem`, `GetMenuItemInfo`, `GetMenuItemCount`, `DrawMenuBar`).
- [x] WXA-1403 (tier-1): Implement additional window-state/geometry APIs not yet shimmed (`SetCapture`, `ReleaseCapture`, `GetWindowRect`, `GetClientRect`, `MapWindowPoints`, `WindowFromPoint`, `ChildWindowFromPointEx`, `GetParent`, `GetWindow`, `GetAncestor`, `IsChild`).
- [x] WXA-1404 (tier-1): Implement invalidation/render queue APIs in USER32 facade (`UpdateWindow`, `InvalidateRect`, `ValidateRect`) with deterministic no-op/safe behavior.
- [ ] WXA-1405 (tier-2): Implement common system metric/accessibility queries with stable defaults (`SystemParametersInfo`, `GetDpiForWindow`, `GetDpiForSystem`, theme/system metrics).
- [ ] WXA-1406 (tier-3, cautious): Implement message-loop callbacks (`SendMessage`, `PostMessage`, `PeekMessage`, `GetMessage`, `DispatchMessage`, `TranslateMessage`) once PAL message pump can guarantee contract fidelity.

## GDI / GDI+ and Resource Handles

- [ ] WXA-1501: Keep device-context and handle methods routed to managed drawing backend; add no-op-safe wrappers for missing legacy queries.
- [ ] WXA-1502: Implement `GetSystemColor`, `SetTextColor`, `SetBkColor`, `GetDeviceCaps` fallback paths for controls that query these frequently.
- [ ] WXA-1503: Add curated GDI+ and cursor/font fallback handling for common property surfaces.
- [ ] WXA-1504: Add resource and image compatibility shims for icon/cursor extraction and `Bitmap` conversion (`LoadImage`, `CreateIconFromResourceEx`, `ImageList` interoperability).

## COMCTL32 and Common Control Helpers

- [ ] WXA-1601: Expand `COMCTL32` shim for image list primitives used by `ListView`, `TreeView`, and image-backed controls (`Create/Read/Draw/Destroy` semantics).
- [ ] WXA-1602: Keep `InitCommonControls` / `InitCommonControlsEx` as deterministic no-op with explicit supported-control feature flags.

## Shell / Resources / Icons / Cursors

- [ ] WXA-1701: Add stock icon and cursor provider service with canonical fallback set (`Application`, default status/error/warning/info/question, shield, folder/file, message/task dialog, scroll/dropdown arrows).
- [ ] WXA-1702: Implement icon extraction/service equivalents for `ExtractAssociatedIcon`, `SHGetStockIconInfo`, shell execute placeholders.
- [ ] WXA-1703: Add and centralize required icons/cursors in WinFormsX resources; ensure `Button.ico`, `ImageInError.ico`, `PropertiesTab.ico`, `ShieldIcon.ico`, and any missing icon/cursor assets are embedded with license notices.
- [ ] WXA-1704: Harden `.resx` load/save and `ResXDataNode` behavior for image/icon/cursor/stream payloads.

## Rich Text and Text Editing

- [ ] WXA-1801: Implement managed `RichTextBox`/`TextBoxBase` fallback for `MsftEdit.DLL`-less environments (selection, link detection, scrolling, RTF read/write).
- [ ] WXA-1802: Add deterministic default font/cursor/input-language interactions used by text editors.

## KERNEL32 / Process / Loader

- [ ] WXA-1304: Implement process/module query compatibility stubs where PAL cannot supply native handles (`GetModuleFileName`, `GetWindowThreadProcessId`, `GetCurrentProcess`, activation context basics).
- [ ] WXA-1305: Implement error reporting compatibility (`GetLastError`/`SetLastError`) for direct-PInvoke consumers.

## Accessibility / UI Automation

- [ ] WXA-1901: Implement fallback provider hooks used by controls (`ListView`, `PropertyGrid`, `ToolStrip`, `ComboBox`, `DataGridView`, `RichTextBox`).
- [ ] WXA-1902: Add accessible event bridge for `UiaClientsAreListening`-style checks and basic property-provider methods.

## SystemInformation / Theme / Power / Misc

- [ ] WXA-2001: Implement `SystemParametersInfo` and `SystemParametersInfoForDpi` compatibility with deterministic defaults where side effects are not available.
- [ ] WXA-2002: Implement `UXTHEME` and `DWMAPI` no-op-safe stubs used by theme rendering.
- [ ] WXA-2003: Add Power status and session change notifications where feasible (`PowerModeChanged`, `SessionSwitch`) from managed sources.

## Diagnostics and Integration

- [ ] WXA-2101: Add hang diagnostics and timeout capture around `UIIntegrationTests` to isolate first real stack for each failing case.
- [ ] WXA-2102: Add an execution plan tracker that marks each remediation against specific integration failure signatures and controls smoke IDs.

## Ongoing Watchlist

- [ ] WXA-WL01: `Buttons` (resource extraction path + catalog form hookup).
- [~] WXA-WL02: `MultipleControls`/`RichTextBoxes`/`TextBoxes` (input/editing path hardening; managed link/range fallback in progress).
- [ ] WXA-WL03: `TreeView, ImageList` / `ListView` / `MDI Parent` / `TrackBars` (native common-control state).
- [ ] WXA-WL04: `Calendar` / `DateTimePicker` (handle and message conversion).
- [ ] WXA-WL05: `ToolStrips` / `Menus` / `ToolStripSeparatorPreferredSize` (layout and message timing).
- [ ] WXA-WL06: `Splitter` / `ScrollableControlsButton` (backend availability and offscreen fallback).
- [ ] WXA-WL07: `MessageBox` / `DockLayout` / `FormOwnerTest` (ownership/modal behavior + assertion diagnostics).

## Active Work Notes

- `WXA-1103` is actively in progress with managed drag/drop loop and target-resolution work.
- Remaining DragDrop failures are now narrowed to drag start/target behavior in specific UI flows; `OLE32.dll`/GIT failures are fixed.

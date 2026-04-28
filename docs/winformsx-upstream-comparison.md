# WinFormsX Compared With Upstream Windows Forms

This document is for contributors who already know the Microsoft
`dotnet/winforms` repository and need to understand what is different in
WinFormsX, where those differences live, and how to make changes without
reintroducing Windows-native dependencies.

The upstream repository currently uses `main`, not `master`. If you have an
upstream remote configured, compare against `upstream/main`. In this checkout,
`origin` points at the WinFormsX fork, not the Microsoft repository.

```sh
git remote add upstream https://github.com/dotnet/winforms.git
git fetch upstream main
git diff upstream/main HEAD -- src/System.Windows.Forms/src/System/Windows/Forms/Control.cs
git show upstream/main:src/System.Windows.Forms/src/System/Windows/Forms/Control.cs
```

## Project Goal

Upstream Windows Forms is a Windows desktop framework. Its runtime wraps Win32,
USER32, GDI, GDI+, COMCTL32, shell dialogs, COM/OLE, system metrics, and Windows
accessibility services.

WinFormsX has a different implementation goal:

```text
WinForms public API and behavior
  -> managed WinForms semantics
  -> WinFormsX PAL
  -> Drawing PAL
  -> Impeller renderer
  -> Silk.NET host window/input
  -> Vulkan surface/swapchain
```

Windows, macOS, and Linux are host operating systems only. Windows is not a
special native-Win32 execution path. WinFormsX must not call real `user32.dll`,
`gdi32.dll`, `gdiplus.dll`, `comctl32.dll`, shell common-dialog APIs, Windows
hooks, native Windows menus, native common controls, or host `SystemEvents`.

Win32-shaped structs, constants, enums, message ids, handles, and return values
are still useful compatibility contracts. The implementation behind those
contracts must be managed PAL state plus Impeller/Silk/Vulkan rendering.

## High-Level Differences

| Area | Upstream `dotnet/winforms` | WinFormsX |
| --- | --- | --- |
| Platform | Windows-only runtime behavior backed by Win32 | Cross-platform runtime behavior backed by PAL |
| Windows handles | Real `HWND`, `HDC`, `HBITMAP`, menus, native controls | Synthetic handles owned by managed registries |
| Message dispatch | USER32 message pump and `SendMessage`/`PostMessage` | Managed message queue and PAL message emulation |
| Drawing | GDI/GDI+ and Windows theming APIs | Managed drawing state routed to Impeller |
| Text | GDI/GDI+/DirectWrite-adjacent Windows behavior depending on path | HarfBuzz shaping and managed/Impeller glyph rendering work |
| Host window | Windows HWND | Silk.NET window with Vulkan surface |
| Renderer | Windows GDI/GDI+/theme rendering | Impeller display lists and Vulkan swapchain |
| Dialogs | Win32/common-item/common-dialog APIs | Must be managed WinFormsX modal forms/services |
| Tests | Mostly Windows runtime assumptions | Original tests plus architecture checks and screenshot regressions |

## Important Fork-Specific Files

### Runtime Provider And PAL

- `src/System.Windows.Forms.Primitives/src/System/Windows/Forms/Platform/PlatformApi.cs`
- `src/System.Windows.Forms.Primitives/src/System/Windows/Forms/Platform/*.cs`
- `src/System.Windows.Forms/src/System/Windows/Forms/Platform/ImpellerPlatformProvider.cs`
- `src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/*.cs`

`PlatformApi` is the central platform entry point. It must initialize to the
Impeller provider only. If a change needs window creation, metrics, timers,
input state, message dispatch, common controls, dialogs, or accessibility, add
or extend a PAL interface and implement it under the Impeller provider.

Do not add OS-specific branches in controls such as `Button`, `ComboBox`,
`ListBox`, `TextBox`, `TabControl`, or `ToolStrip`. Those controls should remain
as close to upstream as possible. The PAL absorbs the difference between a real
Windows machine and the synthetic WinFormsX runtime.

### P/Invoke Boundary

- `src/System.Windows.Forms.Primitives/src/Windows/Win32/`
- `src/System.Private.Windows.Core/src/Windows/Win32/`
- `eng/verify-impeller-only.sh`

Upstream uses CsWin32-generated P/Invoke wrappers as normal runtime calls.
WinFormsX treats Win32-shaped P/Invoke wrappers as an interception boundary.

When a control calls a Win32-shaped API, prefer to intercept at the lowest shared
wrapper and route to `PlatformApi`. This keeps the original control logic intact
and avoids scattering backend checks through thousands of call sites.

New real Windows DLL imports are not allowed. If a Windows-shaped operation is
missing, implement the behavior in PAL code and keep the return values compatible
with Windows semantics.

### Drawing PAL And Impeller

- `src/System.Drawing.Common/src/System/Drawing/Graphics.cs`
- `src/System.Drawing.Common/src/System/Drawing/Backends/*.cs`
- `src/System.Drawing.Common/src/System/Drawing/Backends/Text/*.cs`
- `src/System.Drawing.Common/src/System/Drawing/Impeller/*.cs`
- `src/System.Drawing.Common/src/System/Drawing/Fonts/`

Upstream `System.Drawing.Common` is GDI+-centric. WinFormsX keeps public
`System.Drawing` types compatible, but their internals must become managed state
and Drawing PAL commands. `Graphics`, `Pen`, `Brush`, `Font`, `Image`, `Region`,
`Matrix`, and `StringFormat` should not attempt native GDI+ work in active
WinFormsX paths.

If a drawing API is not implemented yet, fail explicitly or emit a compatibility
warning. Do not silently call GDI+ or invent an OS-native alternate path.

Text is shaped through the HarfBuzz path and rendered through the WinFormsX
drawing backend. Native Impeller paragraph/glyph-atlas APIs are useful, but they
must be enabled only when stable and observable under the screenshot harness.

### Host And Renderer

- `src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/SilkPlatformBackend.cs`
- `src/System.Drawing.Common/src/System/Drawing/Impeller/ImpellerSwapchainManager.cs`
- `src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/ImpellerWindowInterop.cs`
- `eng/fetch-impeller-sdk.sh`

Silk.NET owns the native host window and input stream. Vulkan is the backing
graphics API. Impeller records and presents the rendering work. Do not add a
parallel Windows host, native HWND renderer, native menu path, or OS-specific
common-control renderer.

Frame ownership should remain singular: Silk's render callback drives frame
presentation. WinForms invalidation should mark dirty state and get collapsed
into the render cadence rather than triggering unbounded immediate repaint loops.

### Samples And Screenshot Regression Harness

- `src/WinFormsX.Samples/`
- `eng/capture-winforms-screen.sh`
- `eng/capture-winforms-click-regression.sh`

`WinFormsX.Samples` is the current kitchen-sink and smoke-test surface. It is
not an upstream sample. Use it to validate visible behavior while bringing
controls online through normal WinForms paint and event paths.

The screenshot harness exists because many regressions are visual: blank frames,
stale stretched frames, bad hit testing, missing menu/tab text, or controls that
paint for one frame and then disappear.

## Dialogs: Do Not Use Native Common Dialogs

Upstream common dialogs use Win32 and COM surfaces such as:

- `GetOpenFileName` / `GetSaveFileName`
- Vista `IFileDialog`
- `SHBrowseForFolder` / shell item APIs
- `ChooseColor`
- `ChooseFont`
- `PrintDlg` / page setup APIs
- native message boxes and task dialogs

WinFormsX must replace these with managed dialog services and ordinary WinFormsX
modal forms:

- `MessageBox`
- `OpenFileDialog`
- `SaveFileDialog`
- `FolderBrowserDialog`
- `ColorDialog`
- `FontDialog`
- `PrintDialog`
- `PageSetupDialog`
- `PrintPreviewDialog`
- `TaskDialog`

The public APIs should remain source-compatible. The implementation should
translate properties into managed dialog models, run a WinFormsX modal loop,
disable and restore owner windows, return the expected `DialogResult`, and emit
warnings for unsupported stock options until those options are implemented.

Start with `MessageBox`, then file/folder dialogs, then color/font, then
printing. File and folder dialogs should browse through managed `System.IO` and
PAL-known locations rather than shell COM. Printing should begin with preview or
PDF-like output before platform print-spooler integration is added behind a PAL.

## How To Make Changes

1. Identify whether the change is public API behavior, Win32 message semantics,
   drawing behavior, renderer/host behavior, or tests.
2. Compare the upstream file with `git show upstream/main:<path>` before editing
   a forked control or public API file.
3. Keep public API shapes and documented behavior compatible unless there is a
   deliberate compatibility decision.
4. Put platform behavior behind `PlatformApi` or Drawing PAL interfaces.
5. Intercept Win32-shaped calls at shared wrappers instead of adding backend
   checks inside controls.
6. Add clear warnings for deliberately unsupported or disabled stock behavior.
7. Run architecture checks and visual smoke checks before committing.

Use this command set for routine verification:

```sh
dotnet build src/System.Private.Windows.Core/src/System.Private.Windows.Core.csproj -v:minimal
dotnet build src/System.Drawing.Common/src/System.Drawing.Common.csproj -v:minimal
dotnet build src/System.Windows.Forms/src/System.Windows.Forms.csproj -v:minimal
dotnet build src/WinFormsX.Samples/WinFormsX.Samples.csproj -v:minimal
eng/verify-impeller-only.sh
eng/capture-winforms-click-regression.sh /tmp/winformsx-regression
```

On macOS, Vulkan typically needs the Homebrew Vulkan loader visible at runtime:

```sh
DYLD_LIBRARY_PATH=/opt/homebrew/lib:$DYLD_LIBRARY_PATH \
  dotnet run --project src/WinFormsX.Samples/WinFormsX.Samples.csproj
```

## What Not To Do

- Do not add new `DllImport` or `LibraryImport` calls to Windows DLLs.
- Do not restore `Gdip*` calls as active drawing behavior.
- Do not use host `Microsoft.Win32.SystemEvents`.
- Do not call shell COM or native common-dialog APIs.
- Do not create a Windows-only rendering or hosting path.
- Do not add control-specific drawing shortcuts when the behavior belongs in
  Drawing PAL, message PAL, or renderer code.
- Do not treat Windows as a privileged implementation path. It is only another
  Silk.NET/Vulkan host.

## Reading The Diff Productively

The full repository diff against upstream is noisy because the fork also has
infra, package, test, and source-tree movement. For architecture work, start
with narrow comparisons:

```sh
git diff upstream/main HEAD -- src/System.Windows.Forms.Primitives/src/System/Windows/Forms/Platform
git diff upstream/main HEAD -- src/System.Windows.Forms/src/System/Windows/Forms/Platform
git diff upstream/main HEAD -- src/System.Drawing.Common/src/System/Drawing/Backends
git diff upstream/main HEAD -- src/System.Drawing.Common/src/System/Drawing/Impeller
git diff upstream/main HEAD -- src/System.Windows.Forms/src/System/Windows/Forms/Dialogs
git diff upstream/main HEAD -- eng/verify-impeller-only.sh eng/capture-winforms-click-regression.sh
```

For a specific control, first compare the upstream control file, then trace its
native calls downward into the PAL or P/Invoke boundary. The fix should usually
land below the control, not inside it.

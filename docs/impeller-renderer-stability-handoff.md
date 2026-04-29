# Impeller Renderer Stability Handoff

Last updated: 2026-04-29

This handoff is for the current WinFormsX renderer/resource-exhaustion work. It
does not restate the full project architecture. Read these first for the broader
rules and goals:

- `ARCHITECTURE.md`: PAL-first development rule and the "fix at the P/Invoke/PAL
  boundary, not inside controls" principle.
- `IMPELLER_HANDOFF.md`: Impeller SDK bootstrap, Vulkan/MoltenVK runtime setup,
  and earlier startup blockers.
- `docs/winformsx-upstream-comparison.md`: how this fork differs from Microsoft
  `dotnet/winforms`, including the Impeller-only/Silk.NET/Vulkan runtime policy.
- `docs/building.md` and `docs/testing.md`: repository-level build and test
  conventions.

## Current Goal

Make the Kitchen Sink sample usable under the single supported runtime:

```text
WinForms semantics -> WinFormsX PAL -> Drawing PAL -> Impeller -> Silk.NET -> Vulkan
```

The immediate blocker is renderer stability. Clicking, hovering, switching tabs,
and resizing must not produce blank/white/black frames, missing text, descriptor
pool errors, process crashes, or stale stretched content.

## Current Status

The app is stable for normal exploratory use on the Impeller/Silk.NET/Vulkan
path. The previous renderer-wide failures that caused blank client frames,
missing text, `ErrorFragmentedPool` spam, and crashes during normal clicking,
hovering, tab switching, textbox input, and frame stress have been resolved.

The current stability follow-up is native cursor loading. Scrolling or hovering
around list/splitter areas exposed a `USER32.dll` load through `Cursors.Default`.
Project policy is consistent behavior on all OSes: cursor behavior should be
synthetic everywhere, not native on Windows and synthetic elsewhere.

## Current Git State

The current work branch in the `winforms/` submodule is:

```sh
git -C /Volumes/Dev/code/jagtesh/UniWinForms/winforms branch --show-current
```

Expected branch at handoff time:

```text
codex/winformsx-stability
```

Latest completed stability checkpoint:

```text
5b39b27 fix: stabilize impeller descriptor pool allocation
```

## Completed Stability Work

The descriptor-pool/whiteout issue was resolved as a renderer integration bug,
not as a control-specific bug.

What was learned:

- Frame scheduling and text draw ordering improvements helped but did not fully
  solve resource exhaustion.
- Native paragraph caching reduced construction churn but did not address the
  actual backend allocation failure.
- Diagnostic text modes proved that full native paragraph command pressure was
  the trigger: geometry-only and one-static-paragraph runs passed, while full
  text-heavy frames failed.
- Managed glyph-outline fallback was worse because it turned text into many path
  commands.
- The Impeller SDK retried descriptor allocation only for
  `ErrorOutOfPoolMemory`; this app was hitting `ErrorFragmentedPool`.

Completed fix:

- `eng/patch-impeller-descriptor-pool-retry.sh` patches the downloaded macOS
  arm64 Impeller SDK so descriptor-set allocation retries once on any allocation
  failure.
- `eng/fetch-impeller-sdk.sh` applies that patch to both the cached SDK copy and
  the sample runtime copy.
- `ImpellerRenderingBackend` keeps frame/resource tracing, bounded paragraph
  caching, diagnostic text modes, and deferred frame-resource retirement.
- Default rendering now uses native Impeller paragraphs with no text draw
  budget.

## Exact Reproduction Commands

Run from:

```sh
cd /Volumes/Dev/code/jagtesh/UniWinForms/winforms
```

Build the active runtime path:

```sh
dotnet build src/System.Windows.Forms/src/System.Windows.Forms.csproj -v:minimal
```

Run the current basic-controls stress harness:

```sh
eng/capture-winforms-basic-controls-regression.sh /tmp/winformsx_basic_controls_regression
```

Expected current result: pass, with no descriptor-pool errors, no process crash,
and no blank client capture.

Useful artifacts:

```text
/tmp/winformsx_capture_run.log
/tmp/winformsx_paint_trace.log
/tmp/winformsx_basic_controls_regression/basic-ready.png
/tmp/winformsx_basic_controls_regression/after-rapid-toggle.png
```

Run broader frame/input regressions:

```sh
eng/capture-winforms-frame-stress-regression.sh /tmp/winformsx_frame_stress_regression
eng/capture-winforms-hover-regression.sh /tmp/winformsx_hover_regression
eng/capture-winforms-usability-regression.sh /tmp/winformsx_usability_regression
eng/capture-winforms-textbox-regression.sh /tmp/winformsx_textbox_regression
```

Run architecture checks:

```sh
eng/verify-impeller-only.sh
```

On macOS, make sure the Vulkan loader is visible when running manually:

```sh
DYLD_LIBRARY_PATH=/opt/homebrew/lib:$DYLD_LIBRARY_PATH \
  dotnet run --project src/WinFormsX.Samples/WinFormsX.Samples.csproj --no-build
```

## Harness Caveat To Fix First

`eng/capture-winforms-basic-controls-regression.sh` currently has a weak
nonblank check. The `stress_dark_pixels` metric can pass by counting window
chrome/shadow or non-client pixels even when the client area is blank.

Before relying on the script, update it to crop only the client content below
the title bar/menu/tab strip. Use the stricter approach already present in:

```text
eng/capture-winforms-frame-stress-regression.sh
eng/capture-winforms-hover-regression.sh
```

The basic-controls script should fail if:

- client crop standard deviation is too low;
- client crop unique-color count is too low;
- tab text score is too low;
- a baseline had visible content but a later image lost most visible content;
- logs contain `ErrorFragmentedPool`, `Paint ERROR`, `EndFrame error`,
  `ThreadException`, `DllNotFound`, `Program crashed`, or `Bus error`.

## Diagnostic Findings

### 2026-04-28 Descriptor Pool Fix, Historical

Root cause isolation showed that the crash/blanking path was not general
surface lifetime, geometry, or a single control:

- `WINFORMSX_IMPELLER_TEXT_MODE=none` passed.
- `WINFORMSX_IMPELLER_TEXT_MODE=static` passed.
- Native paragraph rendering with the full Basic Controls frame failed around
  the 32 paragraph-draw range.
- Managed glyph-outline text was worse because it converted text into many path
  draws.

The matching Impeller source for this SDK only retried descriptor-set allocation
when Vulkan returned `ErrorOutOfPoolMemory`. The observed failure was
`ErrorFragmentedPool`, so Impeller logged the error and carried on with an
incomplete frame instead of allocating a fresh per-frame descriptor pool.

`eng/patch-impeller-descriptor-pool-retry.sh` patches the downloaded macOS arm64
`libimpeller.dylib` so descriptor allocation retries once on any non-success
result, then reports the error if the second allocation still fails. This keeps
the normal success path unchanged and handles the Vulkan fragmentation result in
the same way as pool exhaustion.

`eng/fetch-impeller-sdk.sh` now applies that patch to both the cached SDK copy
and the sample runtime copy after download/extraction. The local artifact was
also patched in place for verification.

Verification after the patch:

```sh
dotnet build src/System.Windows.Forms/src/System.Windows.Forms.csproj -v:minimal
eng/verify-impeller-only.sh
WINFORMSX_TRACE_FILE=/tmp/winformsx_paint_trace.log \
  eng/capture-winforms-basic-controls-regression.sh /tmp/winformsx_basic_controls_regression_patched
eng/capture-winforms-frame-stress-regression.sh /tmp/winformsx_frame_stress_regression_patched
eng/capture-winforms-hover-regression.sh /tmp/winformsx_hover_regression_patched
eng/capture-winforms-usability-regression.sh /tmp/winformsx_usability_regression_patched
eng/capture-winforms-textbox-regression.sh /tmp/winformsx_textbox_regression_patched
```

The Basic Controls run rendered full native paragraph text with
`textDraw=38`, `textSkip=0`, and `textBudget=unlimited` on the heavy frames.
No `ErrorFragmentedPool`, `Paint ERROR`, `EndFrame error`, crash, or blank-client
failure signatures were present after the verification runs.

The pre-fix failing runs showed:

- First correct frame on Basic Controls was visually good.
- Later final capture was a blank beige client area.
- `/tmp/winformsx_capture_run.log` contained thousands of descriptor-pool errors.
- `/tmp/winformsx_paint_trace.log` showed repeated successful managed paint-tree
  traversal. The managed paint path did not obviously stop walking controls.

That means the WinForms tree can still be walked, but the presented GPU result
could become incomplete/empty after Impeller reported descriptor allocation
failures.

If descriptor-pool errors or whiteout return, treat them as shared renderer
failures first. Do not chase CheckBox, RadioButton, DataGridView, TabControl, or
MenuStrip polish until the shared frame/resource path is healthy again.

### 2026-04-29 Synthetic Cursor Follow-Up

The current crash report did not originate from Impeller allocation. It came
from `Cursors.Default` loading the Win32 stock cursor through `USER32.dll` while
handling mouse movement around list/splitter UI. That violates the project rule:
cross-platform behavior should be consistent and synthetic on all OSes, with no
native cursor P/Invoke path.

Expected direction:

- `Cursors.*` must create synthetic handles without loading stock OS cursors.
- `Cursor.Current`, `Clip`, `Position`, `Hide`, and `Show` should be managed
  no-ops/placeholders until a real cross-platform cursor PAL exists.
- File/stream cursor constructors may preserve raw cursor bytes for
  serialization, but should not parse through native image/COM APIs.
- Regression logs should fail on `USER32.dll`, `DllNotFound`, `LoadCursor`,
  `CopyCursor`, `DestroyCursor`, `GetIconInfo`, `DrawIcon`, or other native
  cursor calls.

## Likely Failure Classes

Investigate these in order.

### 1. Display List Or Surface Lifetime

Relevant files:

```text
src/System.Drawing.Common/src/System/Drawing/Backends/ImpellerRenderingBackend.cs
src/System.Drawing.Common/src/System/Drawing/Impeller/DisplayListBuilder.cs
src/System.Drawing.Common/src/System/Drawing/Impeller/NativeMethods.cs
src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/SilkPlatformBackend.cs
src/System.Drawing.Common/src/System/Drawing/Impeller/ImpellerSwapchainManager.cs
```

Questions:

- Is every acquired surface released exactly once?
- Does `ImpellerSurfacePresent(surface)` transfer ownership or require explicit
  post-present release?
- Does `ImpellerVulkanSwapchainAcquireNextSurfaceNew` return an object that must
  outlive presentation differently from wrapped FBO surfaces?
- Are display lists retaining paragraph/paint/path objects correctly?
- Are we releasing display-list dependencies too early, or holding them forever?
- Does drawing a failed display list still present a cleared frame?

Next step:

- Add frame ids and native handle ids to trace logs around Acquire, Build,
  DrawDisplayList, Present, DisplayListRelease, SurfaceRelease.
- Log success/failure of each native call and skip presentation if any draw call
  fails.

### 2. Descriptor Pool Fragmentation From Command Volume

Relevant files:

```text
src/System.Drawing.Common/src/System/Drawing/Backends/ImpellerRenderingBackend.cs
src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/ImpellerWindowInterop.cs
```

Questions:

- How many paragraphs, paths, paints, clips, saves/restores, and display-list ops
  are emitted per frame?
- Does repeated full-tree repaint emit an unbounded number of path/text objects?
- Are hidden/offscreen controls still contributing GPU work?
- Are nested clips creating expensive descriptor state?

Next step:

- Add frame counters for text ops, paragraphs created/reused, path builders,
  paths, paints, clips, save/restore depth, and skipped offscreen controls.
- Fail a frame before presentation if budgets are exceeded, leaving the last good
  frame visible.

### 3. Text Rendering Backend

Relevant files:

```text
src/System.Drawing.Common/src/System/Drawing/Backends/Text/HarfBuzzTextEngine.cs
src/System.Drawing.Common/src/System/Drawing/Backends/ImpellerRenderingBackend.cs
src/System.Drawing.Common/src/System/Drawing/Impeller/TypographyProvider.cs
src/System.Drawing.Common/src/System/Drawing/Fonts/
```

Known text history:

- Managed HarfBuzz glyph-outline rendering restored visible text, but produced
  heavy path workloads and crude/unstable UI rendering in earlier iterations.
- Native Impeller paragraphs produce better-looking text, but currently correlate
  with descriptor-pool failures and later whiteouts.
- Caching native paragraphs did not eliminate the failure.

Next step:

- Add a runtime diagnostic switch that disables only text draw calls while
  preserving all geometry. If descriptor errors disappear, text/glyph atlas is
  confirmed as trigger. If they remain, the issue is more general.
- Add a second diagnostic switch that draws one static paragraph per frame. This
  isolates "paragraph draw itself" from "many UI text draws".
- Do not ship a no-text mode except as diagnostic infrastructure.

### 4. Swapchain/Frame Cadence

Relevant files:

```text
src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/ImpellerWindowInterop.cs
src/System.Windows.Forms/src/System/Windows/Forms/Platform/Impeller/SilkPlatformBackend.cs
```

Questions:

- Are we presenting more than one frame for a single invalidation cycle?
- Are input handlers marking dirty immediately while the render callback is still
  working?
- Are failed frames presented instead of preserving the last good frame?
- Are framebuffer-size changes causing stale/corrupt swapchain surfaces?

Next step:

- Track one `FrameState` object per top-level window.
- Separate `dirty`, `rendering`, `presented`, and `failed` states.
- If a frame fails after `BeginFrame`, call `AbortFrame`, do not present, and
  keep the last good frame.
- Add trace lines for why each frame was scheduled.

## Suggested Immediate TDD Sequence

1. Add/keep a regression that moves the mouse over splitter/list surfaces and
   scrolls the list view.
2. Assert that the run does not load `USER32.dll` or hit native cursor APIs.
3. Keep the broader renderer regressions green:
   - Basic Controls;
   - frame stress;
   - hover stress;
   - usability;
   - textbox input.
4. Fix the basic-controls screenshot oracle so it fails on client whiteout.
5. If descriptor or whiteout failures return, rerun the text/geometry diagnostic
   switch matrix from the historical notes before changing individual controls.
6. Commit each passing checkpoint with `type: summary` format.

## "Flutter-Like" Direction

The current immediate-mode full repaint is too naive for a complex WinForms tree.
Mature UI renderers generally avoid rebuilding and uploading everything on every
minor input event.

Borrow these concepts, but keep them adapted to WinForms semantics:

- A single frame scheduler.
- Dirty-region or dirty-subtree tracking.
- Retained render objects/layers for mostly static UI.
- Stable native resources keyed by content/style, with explicit lifetime.
- Frame-local command buffers separated from retained GPU/text/font resources.
- Back-pressure: if the renderer cannot allocate resources, skip presentation
  rather than presenting a cleared frame.
- Instrumented budgets for ops, text, clips, paths, and GPU resource creation.

Do not add a second renderer or fallback renderer. The supported path remains
Impeller/Silk.NET/Vulkan.

## Future Work After Renderer Stabilization

Keep the original project sequence in `ARCHITECTURE.md` and
`docs/winformsx-upstream-comparison.md` as the authority. The next tasks below
are specific to the point after renderer stability is recovered.

### Rendering And Drawing PAL

- Remove remaining GDI+/GDI native fallback paths from active WinFormsX builds.
- Convert more `System.Drawing` objects to managed state plus Drawing PAL
  commands.
- Implement image texture upload and `Graphics.DrawImage`.
- Complete clipping, regions, transforms, pens, brushes, and path semantics.
- Add warnings for every unsupported stock drawing feature.

### Text

- Finish one complete text stack that is always used. Avoid ambiguous fallback
  terminology and hidden alternate paths.
- Keep HarfBuzz for shaping/measurement where appropriate.
- Use native Impeller text only once descriptor/resource lifetime is understood
  and regression-protected.
- Add caret, selection, password masking, IME warnings, clipboard, and keyboard
  editing behavior for `TextBox`.
- Add font substitution documentation for the embedded OSS font roster.

### Input And Controls

- Finish managed mouse capture and focus state.
- Ensure clicks land on the exact visual control under the cursor under HiDPI.
- Button pressed/default/flat/accent states.
- CheckBox/RadioButton rapid-click idempotence.
- One active ComboBox dropdown per top-level window.
- One active menu state machine.
- SplitContainer splitter drag.
- Scrollbars, clipping, and virtualization for ListBox/ListView/TreeView/DataGridView.
- Restore Drawing tab vector primitives through normal `OnPaint`.

### Dialogs

Do not use OS dialogs if the project policy remains Impeller-only. The managed
dialog plan is:

- modal host and service interfaces;
- `MessageBox`;
- `ColorDialog`;
- `FontDialog`;
- `FolderBrowserDialog`;
- `OpenFileDialog` / `SaveFileDialog`;
- print preview / PDF path;
- real printer integration only behind a PAL later.

Reference the dialog section in `docs/winformsx-upstream-comparison.md` before
starting this work.

### Tests And CI

- Make `eng/verify-impeller-only.sh` mandatory in the local pre-commit/check
  workflow.
- Add screenshot tests for:
  - launch;
  - tab click;
  - Draw tab shapes;
  - Data tab text retention;
  - menu click;
  - combo dropdown isolation;
  - rapid checkbox/radio clicks;
  - resize;
  - hover stress;
  - text input/caret;
  - splitter drag.
- Run targeted original tests where they validate public WinForms behavior
  without requiring native OS UI.
- Add tests that fail if logs include `ErrorFragmentedPool`, native DLL load
  attempts, `Paint ERROR`, `EndFrame error`, or process crashes.

## Practical Rules For The Next Agent

- If descriptor-pool errors return during normal tab/mouse use, fix the shared
  renderer/resource path before chasing individual control polish.
- Cursor behavior must remain synthetic and consistent on all OSes until a
  cross-platform cursor PAL is designed.
- Do not introduce real Win32, GDI, GDI+, COMCTL32, shell, or OS common-dialog
  calls.
- Do not add control-specific backend branches unless they are temporary
  diagnostics and are removed before merging.
- Preserve public WinForms API compatibility.
- Prefer low-level PAL/PInvoke interception over edits in upstream controls.
- Keep screenshots and logs as acceptance evidence, not just builds.
- Commit often, but clearly label hypotheses or WIP changes if they do not pass
  the screenshot regression harness.

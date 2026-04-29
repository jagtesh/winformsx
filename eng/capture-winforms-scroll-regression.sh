#!/bin/zsh
set -euo pipefail

OUT_DIR="${1:-/tmp/winformsx_scroll_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
WHEEL_SCRIPT="/tmp/winformsx_scroll_wheel.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_DIR"

pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
sleep 0.3

: > "$RUN_LOG"
: > "$TRACE_LOG"

eng/capture-winforms-screen.sh "$OUT_DIR/before.png" >/tmp/winformsx_scroll_regression.out

WINFORMSX_CLICK_POINTS='0.372,0.105' \
WINFORMSX_AFTER_CLICK_DELAY="${WINFORMSX_AFTER_CLICK_DELAY:-0.8}" \
WINFORMSX_REUSE_RUNNING=1 \
  eng/capture-winforms-click-regression.sh "$OUT_DIR/lists-tab" >>/tmp/winformsx_scroll_regression.out

cat > "$WHEEL_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let normalizedX = Double(ProcessInfo.processInfo.environment["WINFORMSX_SCROLL_X"] ?? "0.25") ?? 0.25
let normalizedY = Double(ProcessInfo.processInfo.environment["WINFORMSX_SCROLL_Y"] ?? "0.52") ?? 0.52

func isTarget(owner: String, name: String) -> Bool {
    owner == "WinFormsX.Samples" || owner == "WinFormsXHarness" || name == "WINFORMSX_AUTOMATION_WINDOW"
}

let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
for window in list {
    let owner = (window[kCGWindowOwnerName as String] as? String) ?? ""
    let name = (window[kCGWindowName as String] as? String) ?? ""
    guard isTarget(owner: owner, name: name) else { continue }

    let bounds = (window[kCGWindowBounds as String] as? [String: Any]) ?? [:]
    let x = CGFloat((bounds["X"] as? Double) ?? 0)
    let y = CGFloat((bounds["Y"] as? Double) ?? 0)
    let width = CGFloat((bounds["Width"] as? Double) ?? 0)
    let height = CGFloat((bounds["Height"] as? Double) ?? 0)
    guard width > 0 && height > 0 else { continue }

    let point = CGPoint(
        x: x + width * CGFloat(max(0.0, min(1.0, normalizedX))),
        y: y + height * CGFloat(max(0.0, min(1.0, normalizedY))))

    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(100_000)

    for _ in 0..<20 {
        CGEvent(scrollWheelEvent2Source: nil, units: .pixel, wheelCount: 2, wheel1: -120, wheel2: 0, wheel3: 0)?.post(tap: .cghidEventTap)
        usleep(30_000)
    }

    print("[SCROLL_OK] posted wheel at \(Int(point.x)),\(Int(point.y)) owner=\(owner) name=\(name)")
    exit(0)
}

print("[SCROLL_ERROR] target window not found")
exit(1)
SWIFT

swift "$WHEEL_SCRIPT" >>/tmp/winformsx_scroll_regression.out
sleep "${WINFORMSX_AFTER_SCROLL_DELAY:-1.0}"

if ! pgrep -f "WinFormsX.Samples" >/dev/null 2>&1; then
  cat /tmp/winformsx_scroll_regression.out
  echo "[SCROLL_REGRESSION_FAIL] sample exited after wheel input"
  tail -n 120 "$RUN_LOG" 2>/dev/null || true
  exit 1
fi

if rg -n "PAL_SEHException|ThreadException|Paint error|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception|USER32\\.dll" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_scroll_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_scroll_regression.out
  cat /tmp/winformsx_scroll_regression_errors.log
  echo "[SCROLL_REGRESSION_FAIL] runtime log contains errors"
  exit 1
fi

AFTER_SCROLL="$OUT_DIR/after-scroll.png"
eng/capture-winforms-screen.sh "$AFTER_SCROLL" >>/tmp/winformsx_scroll_regression.out

python3 - "$OUT_DIR/lists-tab/after-click-1.png" "$AFTER_SCROLL" <<'PY'
import sys
from PIL import Image, ImageChops, ImageStat

before = Image.open(sys.argv[1]).convert("RGB")
after = Image.open(sys.argv[2]).convert("RGB")
if before.size != after.size:
    raise SystemExit(
        f"[SCROLL_REGRESSION_FAIL] before/after capture size mismatch {before.size} != {after.size}")

width, height = before.size
# Available Employees ListBox client area on the Lists & Combos tab.
box = (
    int(width * 0.075),
    int(height * 0.375),
    int(width * 0.310),
    int(height * 0.735))

diff = ImageChops.difference(before.crop(box), after.crop(box))
score = sum(ImageStat.Stat(diff).mean) / 3.0
if score < 3.0:
    raise SystemExit(
        f"[SCROLL_REGRESSION_FAIL] wheel input did not visibly scroll the ListBox (diff={score:.3f})")

print(f"[SCROLL_VISUAL_OK] listbox_diff={score:.3f}")
PY

cat /tmp/winformsx_scroll_regression.out
echo "[SCROLL_REGRESSION_OK] $OUT_DIR"

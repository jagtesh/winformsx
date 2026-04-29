#!/bin/zsh
set -euo pipefail

OUT_DIR="${1:-/tmp/winformsx_input_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
INPUT_SCRIPT="/tmp/winformsx_input_regression.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_DIR"

pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
sleep 0.3

: > "$RUN_LOG"
: > "$TRACE_LOG"

eng/capture-winforms-screen.sh "$OUT_DIR/launch.png" >/tmp/winformsx_input_regression.out

cat > "$INPUT_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"

func isTarget(owner: String, name: String) -> Bool {
    owner == "WinFormsX.Samples" || owner == "WinFormsXHarness" || name == targetTitle
}

func targetWindow() -> CGRect? {
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
        if width > 0 && height > 0 {
            return CGRect(x: x, y: y, width: width, height: height)
        }
    }

    return nil
}

func point(_ rect: CGRect, _ nx: Double, _ ny: Double) -> CGPoint {
    CGPoint(
        x: rect.minX + rect.width * CGFloat(max(0.0, min(1.0, nx))),
        y: rect.minY + rect.height * CGFloat(max(0.0, min(1.0, ny))))
}

func click(_ rect: CGRect, _ nx: Double, _ ny: Double) {
    let p = point(rect, nx, ny)
    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(50_000)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(40_000)
    CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(160_000)
}

func key(_ code: CGKeyCode) {
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true)?.post(tap: .cghidEventTap)
    usleep(35_000)
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false)?.post(tap: .cghidEventTap)
    usleep(90_000)
}

func chord(_ modifier: CGKeyCode, _ code: CGKeyCode) {
    CGEvent(keyboardEventSource: nil, virtualKey: modifier, keyDown: true)?.post(tap: .cghidEventTap)
    usleep(35_000)
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true)?.post(tap: .cghidEventTap)
    usleep(35_000)
    CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false)?.post(tap: .cghidEventTap)
    usleep(35_000)
    CGEvent(keyboardEventSource: nil, virtualKey: modifier, keyDown: false)?.post(tap: .cghidEventTap)
    usleep(120_000)
}

func wheel(_ rect: CGRect, _ nx: Double, _ ny: Double) {
    let p = point(rect, nx, ny)
    CGEvent(mouseEventSource: nil, mouseType: .mouseMoved, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(80_000)
    for _ in 0..<10 {
        CGEvent(scrollWheelEvent2Source: nil, units: .pixel, wheelCount: 2, wheel1: -120, wheel2: 0, wheel3: 0)?.post(tap: .cghidEventTap)
        usleep(35_000)
    }
}

guard let rect = targetWindow() else {
    print("[INPUT_ERROR] target window not found")
    exit(1)
}

click(rect, 0.226, 0.125) // Basic Controls.
click(rect, 0.165, 0.322) // TextBox.
chord(55, 0)              // Command+A.
print("[INPUT_STEP] textbox-select-all")
usleep(300_000)

click(rect, 0.957, 0.125) // Data tab.
click(rect, 0.840, 0.305) // ListView row.
key(125)
key(125)
key(125)
wheel(rect, 0.840, 0.305)
print("[INPUT_STEP] listview-key-wheel")
usleep(300_000)

click(rect, 0.240, 0.670) // DataGridView row.
key(125)
key(125)
print("[INPUT_STEP] datagrid-key")
usleep(300_000)

click(rect, 0.520, 0.125) // Layout tab.
click(rect, 0.785, 0.805) // ScrollBar track in the sample.
key(125)
key(121)
print("[INPUT_STEP] scrollbar-click-key")
usleep(300_000)
SWIFT

swift "$INPUT_SCRIPT" >>/tmp/winformsx_input_regression.out

eng/capture-winforms-screen.sh "$OUT_DIR/after-input.png" >>/tmp/winformsx_input_regression.out

if ! pgrep -f "WinFormsX.Samples" >/dev/null 2>&1; then
  cat /tmp/winformsx_input_regression.out
  echo "[INPUT_REGRESSION_FAIL] sample exited during input regression"
  tail -n 120 "$RUN_LOG" 2>/dev/null || true
  exit 1
fi

if rg -n "PAL_SEHException|ThreadException|Paint error|Paint ERROR|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception|USER32\\.dll|libUSER32" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_input_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_input_regression.out
  cat /tmp/winformsx_input_regression_errors.log
  echo "[INPUT_REGRESSION_FAIL] runtime log contains errors"
  exit 1
fi

python3 - "$OUT_DIR/after-input.png" <<'PY'
import sys
from PIL import Image, ImageStat

image = Image.open(sys.argv[1]).convert("RGB")
width, height = image.size
if width < 200 or height < 200:
    raise SystemExit("[INPUT_REGRESSION_FAIL] capture too small")

# The final Layout tab should retain visible client content after mixed keyboard,
# wheel, and scrollbar input.
client = image.crop((int(width * 0.04), int(height * 0.15), int(width * 0.96), int(height * 0.90)))
stat = ImageStat.Stat(client)
std = sum(stat.stddev) / 3.0
unique = len(set(client.resize((220, 160)).getdata()))
if std < 12.0 or unique < 120:
    raise SystemExit(f"[INPUT_REGRESSION_FAIL] final client capture looks blank/stale (std={std:.1f} unique={unique})")

print(f"[INPUT_VISUAL_OK] std={std:.1f} unique={unique}")
PY

cat /tmp/winformsx_input_regression.out
echo "[INPUT_REGRESSION_OK] $OUT_DIR"

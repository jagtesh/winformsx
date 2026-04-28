#!/bin/zsh
set -euo pipefail

OUT_ROOT="${1:-/tmp/winformsx_basic_controls_regression}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
MOUSE_SCRIPT="/tmp/winformsx_basic_controls_mouse.swift"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_ROOT"

pkill -f "WinFormsX.Samples" >/dev/null 2>&1 || true
sleep 0.3

cat > "$MOUSE_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"
let normalizedX = Double(ProcessInfo.processInfo.environment["WINFORMSX_MOUSE_X"] ?? "0.50") ?? 0.50
let normalizedY = Double(ProcessInfo.processInfo.environment["WINFORMSX_MOUSE_Y"] ?? "0.35") ?? 0.35
let mode = ProcessInfo.processInfo.environment["WINFORMSX_MOUSE_MODE"] ?? "click"

func isTarget(owner: String, name: String) -> Bool {
    owner == "WinFormsX.Samples" || owner == "WinFormsXHarness" || name == targetTitle
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
    usleep(40_000)
    if mode == "down" || mode == "click" {
        CGEvent(mouseEventSource: nil, mouseType: .leftMouseDown, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    }
    if mode == "click" {
        usleep(50_000)
    }
    if mode == "up" || mode == "click" {
        CGEvent(mouseEventSource: nil, mouseType: .leftMouseUp, mouseCursorPosition: point, mouseButton: .left)?.post(tap: .cghidEventTap)
    }

    print("[MOUSE_\(mode.uppercased())_OK] \(Int(point.x)),\(Int(point.y)) owner=\(owner) name=\(name)")
    exit(0)
}

print("[MOUSE_ERROR] target window not found")
exit(1)
SWIFT

post_mouse() {
  local mode="$1"
  local point="$2"
  WINFORMSX_MOUSE_MODE="$mode" \
  WINFORMSX_MOUSE_X="${point%,*}" \
  WINFORMSX_MOUSE_Y="${point#*,}" \
    swift "$MOUSE_SCRIPT"
}

BASELINE="$OUT_ROOT/basic-ready.png"
PRESSED="$OUT_ROOT/button-pressed.png"
RELEASED="$OUT_ROOT/button-released.png"
AFTER_STRESS="$OUT_ROOT/after-rapid-toggle.png"

eng/capture-winforms-screen.sh "$OUT_ROOT/launch.png"
post_mouse click "0.226,0.108"
sleep 0.5
eng/capture-winforms-screen.sh "$BASELINE"

post_mouse down "0.080,0.500"
sleep "${WINFORMSX_PRESS_CAPTURE_DELAY:-0.16}"
eng/capture-winforms-screen.sh "$PRESSED"
post_mouse up "0.080,0.500"
sleep 0.3
eng/capture-winforms-screen.sh "$RELEASED"

for _ in {1..5}; do
  post_mouse click "0.545,0.225"
  post_mouse click "0.545,0.288"
  post_mouse click "0.705,0.288"
  post_mouse click "0.545,0.354"
done

sleep 0.5
eng/capture-winforms-screen.sh "$AFTER_STRESS"

python3 - "$BASELINE" "$PRESSED" "$AFTER_STRESS" <<'PY'
import sys
from PIL import Image, ImageStat

baseline_path, pressed_path, stress_path = sys.argv[1:]

def crop_mean(path, box):
    image = Image.open(path).convert("RGB")
    width, height = image.size
    crop = image.crop((
        int(width * box[0]),
        int(height * box[1]),
        int(width * box[2]),
        int(height * box[3])))
    stat = ImageStat.Stat(crop)
    return sum(stat.mean) / 3.0

baseline_button = crop_mean(baseline_path, (0.090, 0.475, 0.160, 0.510))
pressed_button = crop_mean(pressed_path, (0.090, 0.475, 0.160, 0.510))
if pressed_button > baseline_button - 3.0:
    raise SystemExit(
        "[BASIC_CONTROLS_REGRESSION_FAIL] button did not visibly depress "
        f"(baseline={baseline_button:.1f}, pressed={pressed_button:.1f})")

stress_image = Image.open(stress_path).convert("RGB")
width, height = stress_image.size
content = stress_image.crop((int(width * 0.05), int(height * 0.18), int(width * 0.95), int(height * 0.88))).convert("L")
dark_pixels = sum(1 for pixel in content.getdata() if pixel < 90)
if dark_pixels < 8000:
    raise SystemExit(
        "[BASIC_CONTROLS_REGRESSION_FAIL] rapid toggle stress lost visible text/shapes "
        f"(dark_pixels={dark_pixels})")

print(
    "[BASIC_CONTROLS_REGRESSION_OK] "
    f"button baseline={baseline_button:.1f} pressed={pressed_button:.1f}; "
    f"stress_dark_pixels={dark_pixels}")
PY

if rg -in "ThreadException|paint error|EndFrame error|Program crashed|Bus error|DllNotFound|ErrorFragmentedPool|Unhandled exception|StackOverflow" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_basic_controls_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_basic_controls_regression_errors.log
  echo "[BASIC_CONTROLS_REGRESSION_FAIL] runtime log contains errors"
  exit 1
fi

echo "[BASIC_CONTROLS_REGRESSION_ARTIFACTS] $OUT_ROOT"

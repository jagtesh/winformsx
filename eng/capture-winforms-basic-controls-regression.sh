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

check_basic_controls_visual() {
  python3 - "$1" <<'PY'
import sys
from PIL import Image, ImageStat

path = sys.argv[1]

def crop(image, box):
    width, height = image.size
    return image.crop((
        int(width * box[0]),
        int(height * box[1]),
        int(width * box[2]),
        int(height * box[3])))

def metrics(path):
    image = Image.open(path).convert("RGB")
    width, height = image.size
    if width < 400 or height < 300:
        raise SystemExit(
            f"[BASIC_CONTROLS_REGRESSION_FAIL] capture too small: {path} {width}x{height}")

    # Client content only: skip desktop margins, title bar, menu, tab strip, and status bar.
    client = crop(image, (0.060, 0.180, 0.940, 0.900))
    sample = client.resize((300, max(1, int(300 * client.height / client.width))))
    client_stat = ImageStat.Stat(sample)
    client_colors = sample.getcolors(maxcolors=2_000_000) or []

    tab_text = crop(image, (0.220, 0.135, 0.310, 0.160))
    tab_score = sum(1 for r, g, b in tab_text.getdata() if (r + g + b) / 3.0 < 110)

    heading = crop(image, (0.075, 0.205, 0.300, 0.245))
    heading_blue = sum(1 for r, g, b in heading.getdata() if b > 130 and r < 100 and g < 180)

    client_dark = sum(1 for r, g, b in client.getdata() if (r + g + b) / 3.0 < 90)

    return {
        "std": sum(client_stat.stddev) / 3.0,
        "unique": len(client_colors),
        "tab_score": tab_score,
        "heading_blue": heading_blue,
        "client_dark": client_dark,
    }

m = metrics(path)
failures = []
if m["std"] < 18.0:
    failures.append(f"low client stddev={m['std']:.1f}")
if m["unique"] < 50:
    failures.append(f"low unique color count={m['unique']}")
if m["tab_score"] < 300:
    failures.append(f"tab text score too low={m['tab_score']}")
if m["heading_blue"] < 1_000:
    failures.append(f"Basic Controls heading appears missing={m['heading_blue']}")
if m["client_dark"] < 4_000:
    failures.append(f"client text/shape pixels too low={m['client_dark']}")

if failures:
    raise SystemExit(
        "[BASIC_CONTROLS_REGRESSION_FAIL] " + "; ".join(failures))

print(
    "[BASIC_CONTROLS_VISUAL_READY] "
    f"std={m['std']:.1f} unique={m['unique']} tab_score={m['tab_score']} "
    f"heading_blue={m['heading_blue']} client_dark={m['client_dark']}")
PY
}

BASELINE="$OUT_ROOT/basic-ready.png"
PRESSED="$OUT_ROOT/button-pressed.png"
RELEASED="$OUT_ROOT/button-released.png"
AFTER_STRESS="$OUT_ROOT/after-rapid-toggle.png"

eng/capture-winforms-screen.sh "$OUT_ROOT/launch.png"
post_mouse click "0.226,0.125"
sleep 0.5

baseline_ready=0
for _ in {1..10}; do
  eng/capture-winforms-screen.sh "$BASELINE"
  if check_basic_controls_visual "$BASELINE" >/tmp/winformsx_basic_controls_ready.log 2>&1; then
    baseline_ready=1
    cat /tmp/winformsx_basic_controls_ready.log
    break
  fi

  sleep "${WINFORMSX_BASELINE_RETRY_DELAY:-0.2}"
done

if [[ "$baseline_ready" != "1" ]]; then
  cat /tmp/winformsx_basic_controls_ready.log 2>/dev/null || true
  echo "[BASIC_CONTROLS_REGRESSION_FAIL] baseline Basic Controls capture is not ready"
  exit 1
fi

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

python3 - "$BASELINE" "$PRESSED" "$RELEASED" "$AFTER_STRESS" <<'PY'
import sys
from PIL import Image, ImageStat

baseline_path, pressed_path, released_path, stress_path = sys.argv[1:]

def crop(image, box):
    width, height = image.size
    return image.crop((
        int(width * box[0]),
        int(height * box[1]),
        int(width * box[2]),
        int(height * box[3])))

def crop_mean(path, box):
    image = Image.open(path).convert("RGB")
    stat = ImageStat.Stat(crop(image, box))
    return sum(stat.mean) / 3.0

def visual_metrics(path):
    image = Image.open(path).convert("RGB")
    width, height = image.size
    if width < 400 or height < 300:
        raise SystemExit(
            f"[BASIC_CONTROLS_REGRESSION_FAIL] capture too small: {path} {width}x{height}")

    client = crop(image, (0.060, 0.180, 0.940, 0.900))
    sample = client.resize((300, max(1, int(300 * client.height / client.width))))
    client_stat = ImageStat.Stat(sample)
    client_colors = sample.getcolors(maxcolors=2_000_000) or []
    tab_text = crop(image, (0.220, 0.135, 0.310, 0.160))
    heading = crop(image, (0.075, 0.205, 0.300, 0.245))

    return {
        "std": sum(client_stat.stddev) / 3.0,
        "unique": len(client_colors),
        "tab_score": sum(1 for r, g, b in tab_text.getdata() if (r + g + b) / 3.0 < 110),
        "heading_blue": sum(1 for r, g, b in heading.getdata() if b > 130 and r < 100 and g < 180),
        "client_dark": sum(1 for r, g, b in client.getdata() if (r + g + b) / 3.0 < 90),
    }

def assert_visual(label, actual, baseline):
    failures = []
    if actual["std"] < 18.0:
        failures.append(f"low client stddev={actual['std']:.1f}")
    if actual["unique"] < 50:
        failures.append(f"low unique color count={actual['unique']}")
    if actual["tab_score"] < 300:
        failures.append(f"tab text score too low={actual['tab_score']}")
    if actual["std"] < baseline["std"] * 0.55:
        failures.append(f"lost client variance baseline={baseline['std']:.1f} actual={actual['std']:.1f}")
    if actual["unique"] < baseline["unique"] * 0.55:
        failures.append(f"lost color variety baseline={baseline['unique']} actual={actual['unique']}")
    if actual["tab_score"] < baseline["tab_score"] * 0.65:
        failures.append(f"lost tab text baseline={baseline['tab_score']} actual={actual['tab_score']}")
    if actual["heading_blue"] < baseline["heading_blue"] * 0.65:
        failures.append(f"lost Basic Controls heading baseline={baseline['heading_blue']} actual={actual['heading_blue']}")
    if actual["client_dark"] < baseline["client_dark"] * 0.55:
        failures.append(f"lost client text/shapes baseline={baseline['client_dark']} actual={actual['client_dark']}")

    if failures:
        raise SystemExit(
            f"[BASIC_CONTROLS_REGRESSION_FAIL] {label}: " + "; ".join(failures))

baseline_button = crop_mean(baseline_path, (0.090, 0.475, 0.160, 0.510))
pressed_button = crop_mean(pressed_path, (0.090, 0.475, 0.160, 0.510))
if pressed_button > baseline_button - 3.0:
    raise SystemExit(
        "[BASIC_CONTROLS_REGRESSION_FAIL] button did not visibly depress "
        f"(baseline={baseline_button:.1f}, pressed={pressed_button:.1f})")

baseline = visual_metrics(baseline_path)
pressed = visual_metrics(pressed_path)
released = visual_metrics(released_path)
stress = visual_metrics(stress_path)
assert_visual("button-pressed", pressed, baseline)
assert_visual("button-released", released, baseline)
assert_visual("after-rapid-toggle", stress, baseline)

print(
    "[BASIC_CONTROLS_REGRESSION_OK] "
    f"button baseline={baseline_button:.1f} pressed={pressed_button:.1f}; "
    f"baseline std={baseline['std']:.1f} unique={baseline['unique']} tab_score={baseline['tab_score']} "
    f"heading_blue={baseline['heading_blue']} client_dark={baseline['client_dark']}; "
    f"stress std={stress['std']:.1f} unique={stress['unique']} tab_score={stress['tab_score']} "
    f"heading_blue={stress['heading_blue']} client_dark={stress['client_dark']}")
PY

if rg -in "PAL_SEHException|ThreadException|paint error|EndFrame error|Program crashed|Bus error|DllNotFound|EntryPointNotFound|NativeLibrary|USER32\\.dll|ErrorFragmentedPool|Unhandled exception|StackOverflow" "$RUN_LOG" "$TRACE_LOG" >/tmp/winformsx_basic_controls_regression_errors.log 2>/dev/null; then
  cat /tmp/winformsx_basic_controls_regression_errors.log
  echo "[BASIC_CONTROLS_REGRESSION_FAIL] runtime log contains errors"
  exit 1
fi

echo "[BASIC_CONTROLS_REGRESSION_ARTIFACTS] $OUT_ROOT"

#!/bin/zsh
set -euo pipefail

OUT_PATH="${1:-/tmp/winformsx_window.png}"
RUN_LOG="/tmp/winformsx_capture_run.log"
TRACE_LOG="/tmp/winformsx_paint_trace.log"
FIND_SCRIPT="/tmp/find_winforms_window.swift"

cd "$(dirname "$0")/.."

SYNC_SRC="artifacts/bin/System.Windows.Forms/Debug/net9.0"
SYNC_DST="artifacts/bin/WinFormsX.Samples/Debug/net9.0"
for dll in System.Windows.Forms.dll System.Windows.Forms.Primitives.dll System.Drawing.Common.dll System.Private.Windows.Core.dll Accessibility.dll; do
  if [[ -f "$SYNC_SRC/$dll" ]]; then
    if [[ ! -f "$SYNC_DST/$dll" ]] || ! cmp -s "$SYNC_SRC/$dll" "$SYNC_DST/$dll"; then
      cp "$SYNC_SRC/$dll" "$SYNC_DST/$dll"
    fi
  fi
done

if ! pgrep -f "WinFormsX.Samples" >/dev/null 2>&1; then
  echo "[CAPTURE] launching WinFormsX.Samples..."
  rm -f "$TRACE_LOG"
  DOTNET_BIN="$(command -v dotnet || true)"
  if [[ -z "$DOTNET_BIN" && -x "/usr/local/share/dotnet/dotnet" ]]; then
    DOTNET_BIN="/usr/local/share/dotnet/dotnet"
  fi
  if [[ -z "$DOTNET_BIN" ]]; then
    echo "[CAPTURE_ERROR] dotnet runtime not found"
    exit 1
  fi
  nohup env DYLD_LIBRARY_PATH=/opt/homebrew/lib:${DYLD_LIBRARY_PATH:-} \
    WINFORMSX_TRACE_FILE="$TRACE_LOG" \
    "$DOTNET_BIN" run --project src/WinFormsX.Samples/WinFormsX.Samples.csproj --no-build \
    >"$RUN_LOG" 2>&1 &
fi

cat > "$FIND_SCRIPT" <<'SWIFT'
import CoreGraphics
import Foundation

let targetTitle = "WINFORMSX_AUTOMATION_WINDOW"

func isTarget(owner: String, name: String) -> Bool {
    if owner == "WinFormsX.Samples" || owner == "WinFormsXHarness" {
        return true
    }
    if name == targetTitle {
        return true
    }
    return false
}

for _ in 0..<25 {
    let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
    for w in list {
        let owner = (w[kCGWindowOwnerName as String] as? String) ?? ""
        let name = (w[kCGWindowName as String] as? String) ?? ""
        guard isTarget(owner: owner, name: name) else { continue }

        let id = (w[kCGWindowNumber as String] as? NSNumber)?.intValue ?? -1
        let bounds = (w[kCGWindowBounds as String] as? [String: Any]) ?? [:]
        let x = Int((bounds["X"] as? Double) ?? -1)
        let y = Int((bounds["Y"] as? Double) ?? -1)
        let width = Int((bounds["Width"] as? Double) ?? -1)
        let height = Int((bounds["Height"] as? Double) ?? -1)
        print("[WINFORMS_WINDOW] id=\(id) owner=\(owner) name=\(name) bounds=\(x),\(y) \(width)x\(height)")
        print(id)
        exit(0)
    }
    usleep(200_000)
}

print("[CAPTURE_ERROR] WinForms window not found")
exit(1)
SWIFT

WINDOW_ID="$(swift "$FIND_SCRIPT" | tee /tmp/winformsx_window_detect.log | tail -n 1)"
if [[ -z "${WINDOW_ID}" || "${WINDOW_ID}" == *"CAPTURE_ERROR"* ]]; then
  echo "[CAPTURE] run log tail:"
  tail -n 80 "$RUN_LOG" 2>/dev/null || true
  exit 1
fi

screencapture -x -l "$WINDOW_ID" "$OUT_PATH"
echo "[CAPTURE_OK] ${OUT_PATH}"
if [[ -f "$TRACE_LOG" ]]; then
  echo "[TRACE_LOG] ${TRACE_LOG}"
fi

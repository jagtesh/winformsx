#!/bin/zsh
set -euo pipefail

OUT_ROOT="${1:-/tmp/winformsx_stability_suite}"

cd "$(dirname "$0")/.."
mkdir -p "$OUT_ROOT"

echo "[STABILITY] build System.Windows.Forms"
dotnet build src/System.Windows.Forms/src/System.Windows.Forms.csproj -v:minimal

echo "[STABILITY] build WinFormsX.Samples"
dotnet build src/WinFormsX.Samples/WinFormsX.Samples.csproj -v:minimal

echo "[STABILITY] architecture guard"
eng/verify-impeller-only.sh

echo "[STABILITY] basic controls"
eng/capture-winforms-basic-controls-regression.sh "$OUT_ROOT/basic-controls"

echo "[STABILITY] hover"
eng/capture-winforms-hover-regression.sh "$OUT_ROOT/hover"

echo "[STABILITY] scroll"
eng/capture-winforms-scroll-regression.sh "$OUT_ROOT/scroll"

echo "[STABILITY] input"
eng/capture-winforms-input-regression.sh "$OUT_ROOT/input"

echo "[STABILITY] frame stress"
eng/capture-winforms-frame-stress-regression.sh "$OUT_ROOT/frame-stress"

echo "[STABILITY] usability"
eng/capture-winforms-usability-regression.sh "$OUT_ROOT/usability"

echo "[STABILITY] textbox"
eng/capture-winforms-textbox-regression.sh "$OUT_ROOT/textbox"

echo "[STABILITY_OK] $OUT_ROOT"

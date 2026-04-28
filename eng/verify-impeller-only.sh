#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

failures=0

check() {
  local title="$1"
  local pattern="$2"
  shift 2

  local matches
  matches="$(rg -n "$pattern" "$@" || true)"
  if [[ -n "$matches" ]]; then
    echo "[IMP_ONLY_ERROR] $title"
    echo "$matches"
    failures=$((failures + 1))
  fi
}

runtime_paths=(
  "src/System.Windows.Forms/src"
  "src/System.Windows.Forms.Primitives/src"
  "src/System.Drawing.Common/src"
  "src/System.Private.Windows.Core/src"
)

check \
  "Real Windows DLL imports are not allowed in WinFormsX runtime code." \
  '\[(DllImport|LibraryImport)\("(USER32|user32|GDI32|gdi32|gdiplus|gdiplus\.dll|COMCTL32|comctl32|COMDLG32|comdlg32|KERNEL32|kernel32)\.dll"' \
  "${runtime_paths[@]}"

check \
  "Host SystemEvents are not allowed; route preference/display changes through PAL." \
  '\bSystemEvents\.' \
  "src/System.Windows.Forms/src" \
  "src/System.Drawing.Common/src"

check \
  "Active drawing code must not call GDI+ entrypoints; route through Drawing PAL." \
  '\bPInvoke(Core)?\.Gdip|Gdip\.CheckStatus|GdiPlusInitialization\.EnsureInitialized' \
  "src/System.Windows.Forms/src" \
  "src/System.Drawing.Common/src" \
  "src/System.Private.Windows.Core/src"

if [[ "$failures" -ne 0 ]]; then
  echo "[IMP_ONLY_ERROR] Found $failures Impeller-only architecture violation group(s)."
  exit 1
fi

echo "[IMP_ONLY_OK] WinFormsX runtime passed Impeller-only architecture checks."

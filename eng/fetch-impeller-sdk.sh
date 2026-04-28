#!/usr/bin/env bash
set -euo pipefail

default_sha="3452d735bd38224ef2db85ca763d862d6326b17f"
flutter_sha="${IMPELLER_FLUTTER_SHA:-$default_sha}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

usage() {
  cat <<USAGE
Usage: eng/fetch-impeller-sdk.sh [--sha SHA] [--platform PLATFORM_ARCH] [--output-dir DIR] [--sample-output DIR]

Downloads the Flutter Impeller standalone SDK and copies the native library into
the WinFormsX.Samples runtime probing folder.
USAGE
}

platform_arch=""
output_dir=""
sample_output=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sha)
      flutter_sha="$2"
      shift 2
      ;;
    --platform)
      platform_arch="$2"
      shift 2
      ;;
    --output-dir)
      output_dir="$2"
      shift 2
      ;;
    --sample-output)
      sample_output="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

host_os="$(uname -s)"
host_arch="$(uname -m)"
runtime_id=""

if [[ -z "$platform_arch" ]]; then
  case "$host_os:$host_arch" in
    Darwin:arm64)
      platform_arch="darwin-arm64"
      runtime_id="osx-arm64"
      ;;
    Darwin:x86_64)
      platform_arch="darwin-x64"
      runtime_id="osx-x64"
      ;;
    Linux:aarch64|Linux:arm64)
      platform_arch="linux-arm64"
      runtime_id="linux-arm64"
      ;;
    Linux:x86_64)
      platform_arch="linux-x64"
      runtime_id="linux-x64"
      ;;
    MINGW*:ARM64|MSYS*:ARM64|CYGWIN*:ARM64)
      platform_arch="windows-arm64"
      runtime_id="win-arm64"
      ;;
    MINGW*:x86_64|MSYS*:x86_64|CYGWIN*:x86_64)
      platform_arch="windows-x64"
      runtime_id="win-x64"
      ;;
    *)
      echo "Unsupported host platform: $host_os $host_arch" >&2
      exit 1
      ;;
  esac
fi

if [[ -z "$runtime_id" ]]; then
  case "$platform_arch" in
    darwin-arm64) runtime_id="osx-arm64" ;;
    darwin-x64) runtime_id="osx-x64" ;;
    linux-arm64) runtime_id="linux-arm64" ;;
    linux-x64) runtime_id="linux-x64" ;;
    windows-arm64) runtime_id="win-arm64" ;;
    windows-x64) runtime_id="win-x64" ;;
    *)
      echo "Unsupported platform arch: $platform_arch" >&2
      exit 1
      ;;
  esac
fi

case "$platform_arch" in
  darwin-*) native_lib="libimpeller.dylib" ;;
  linux-*) native_lib="libimpeller.so" ;;
  windows-*) native_lib="impeller.dll" ;;
esac

output_dir="${output_dir:-$repo_root/artifacts/impeller/$platform_arch/$flutter_sha}"
sample_output="${sample_output:-$repo_root/artifacts/bin/WinFormsX.Samples/Debug/net9.0/runtimes/$runtime_id/native}"
archive="$repo_root/artifacts/impeller/$platform_arch/impeller_sdk-$flutter_sha.zip"
url="https://storage.googleapis.com/flutter_infra_release/flutter/$flutter_sha/$platform_arch/impeller_sdk.zip"

mkdir -p "$(dirname "$archive")" "$output_dir" "$sample_output"

if [[ ! -f "$archive" ]]; then
  echo "Downloading $url"
  curl -fL "$url" -o "$archive"
else
  echo "Using cached $archive"
fi

if [[ ! -f "$output_dir/lib/$native_lib" ]]; then
  echo "Extracting to $output_dir"
  unzip -q -o "$archive" -d "$output_dir"
fi

if [[ ! -f "$output_dir/include/impeller.h" ]]; then
  echo "Missing include/impeller.h after extraction" >&2
  exit 1
fi

if [[ ! -f "$output_dir/lib/$native_lib" ]]; then
  echo "Missing lib/$native_lib after extraction" >&2
  exit 1
fi

cp "$output_dir/lib/$native_lib" "$sample_output/$native_lib"
echo "Copied $native_lib to $sample_output"

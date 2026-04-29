param(
  [string] $FlutterSha = $(if ($env:IMPELLER_FLUTTER_SHA) { $env:IMPELLER_FLUTTER_SHA } else { '3452d735bd38224ef2db85ca763d862d6326b17f' }),
  [string] $OutputDir = '',
  [string] $SampleOutput = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$platformArch = 'windows-x64'
$runtimeId = 'win-x64'
$nativeLib = 'impeller.dll'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
  $OutputDir = Join-Path $repoRoot "artifacts/impeller/$platformArch/$FlutterSha"
}

if ([string]::IsNullOrWhiteSpace($SampleOutput)) {
  $SampleOutput = Join-Path $repoRoot "artifacts/bin/WinFormsX.Samples/Debug/net9.0/runtimes/$runtimeId/native"
}

$archive = Join-Path $repoRoot "artifacts/impeller/$platformArch/impeller_sdk-$FlutterSha.zip"
$url = "https://storage.googleapis.com/flutter_infra_release/flutter/$FlutterSha/$platformArch/impeller_sdk.zip"

function Convert-HexStringToByteArray {
  param([string] $Hex)

  if (($Hex.Length % 2) -ne 0) {
    throw "Hex string length must be even."
  }

  $bytes = New-Object byte[] ($Hex.Length / 2)
  for ($i = 0; $i -lt $bytes.Length; $i++) {
    $bytes[$i] = [Convert]::ToByte($Hex.Substring($i * 2, 2), 16)
  }

  return $bytes
}

function Convert-ByteArrayToHexString {
  param([byte[]] $Bytes)

  return (($Bytes | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Test-ByteArrayEqual {
  param(
    [byte[]] $Left,
    [byte[]] $Right
  )

  if ($Left.Length -ne $Right.Length) {
    return $false
  }

  for ($i = 0; $i -lt $Left.Length; $i++) {
    if ($Left[$i] -ne $Right[$i]) {
      return $false
    }
  }

  return $true
}

function Apply-DescriptorPoolRetryPatch {
  param([string] $Path)

  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "Impeller native library not found: $Path"
  }

  $offset = 0x34a01c
  [byte[]] $expected = Convert-HexStringToByteArray '3d782864c47546'
  [byte[]] $replacement = Convert-HexStringToByteArray '85c09090907446'
  $actual = New-Object byte[] $expected.Length

  $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::Read)
  try {
    [void] $stream.Seek($offset, [System.IO.SeekOrigin]::Begin)
    $read = $stream.Read($actual, 0, $actual.Length)
    if ($read -ne $actual.Length) {
      throw "Could not read descriptor-pool retry patch bytes from $Path"
    }

    if (Test-ByteArrayEqual $actual $replacement) {
      Write-Host "Impeller descriptor-pool retry patch already applied: $Path"
      return
    }

    if (-not (Test-ByteArrayEqual $actual $expected)) {
      $actualHex = Convert-ByteArrayToHexString $actual
      $expectedHex = Convert-ByteArrayToHexString $expected
      throw "Impeller descriptor-pool retry patch did not match expected bytes in $Path`n  offset:   0x$($offset.ToString('x'))`n  expected: $expectedHex`n  actual:   $actualHex"
    }

    [void] $stream.Seek($offset, [System.IO.SeekOrigin]::Begin)
    $stream.Write($replacement, 0, $replacement.Length)
  }
  finally {
    $stream.Dispose()
  }

  Write-Host "Applied Impeller descriptor-pool retry patch: $Path"
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $archive), $OutputDir, $SampleOutput | Out-Null

if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) {
  Write-Host "Downloading $url"
  Invoke-WebRequest -Uri $url -OutFile $archive
}
else {
  Write-Host "Using cached $archive"
}

if (-not (Test-Path -LiteralPath (Join-Path $OutputDir "lib/$nativeLib") -PathType Leaf)) {
  Write-Host "Extracting to $OutputDir"
  Expand-Archive -LiteralPath $archive -DestinationPath $OutputDir -Force
}

$header = Join-Path $OutputDir 'include/impeller.h'
$library = Join-Path $OutputDir "lib/$nativeLib"

if (-not (Test-Path -LiteralPath $header -PathType Leaf)) {
  throw "Missing include/impeller.h after extraction"
}

if (-not (Test-Path -LiteralPath $library -PathType Leaf)) {
  throw "Missing lib/$nativeLib after extraction"
}

Apply-DescriptorPoolRetryPatch $library

$sampleLibrary = Join-Path $SampleOutput $nativeLib
Copy-Item -LiteralPath $library -Destination $sampleLibrary -Force
Apply-DescriptorPoolRetryPatch $sampleLibrary

Write-Host "Copied $nativeLib to $SampleOutput"

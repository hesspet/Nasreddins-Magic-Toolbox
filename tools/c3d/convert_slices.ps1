#requires -Version 5.1
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$C3DPath,     # Pfad zu c3d.exe
  [Parameter(Mandatory=$true)][string]$InputRoot,   # Eingabe-Ordner (rekursiv optional)
  [Parameter(Mandatory=$true)][string]$OutputRoot,  # Ausgabe-Ordner

  [ValidateRange(1,10000)]
  [int]$Frames = 50,                                # max. Frames je Richtung

  [ValidateRange(0,100)]
  [int]$LowPercent = 2,                             # Fensterung unten (Perzentil)
  [ValidateRange(0,100)]
  [int]$HighPercent = 98,                           # Fensterung oben (Perzentil)

  # Booleans als object akzeptieren (1/0, true/false, yes/no, on/off)
  [object]$DoAxial    = $true,                      # z-Achse (axial)
  [object]$DoCoronal  = $true,                      # y-Achse (koronal)
  [object]$DoSagittal = $false,                     # x-Achse (sagittal / Seitenansicht)
  [object]$Recurse    = $true                       # Unterordner durchsuchen
)

$ErrorActionPreference = "Stop"

function To-Bool([object]$v) {
  if ($null -eq $v) { return $false }
  if ($v -is [bool]) { return $v }
  $s = $v.ToString().Trim().ToLower()
  switch ($s) {
    { $_ -in @("1","true","t","yes","y","on") }  { return $true }
    { $_ -in @("0","false","f","no","n","off") } { return $false }
    default { throw "Ungültiger Bool-Wert: '$v' (erlaubt: 1/0/true/false/yes/no/on/off)" }
  }
}

# Eingaben zu echten Booleans normalisieren
$DoAxial    = To-Bool $DoAxial
$DoCoronal  = To-Bool $DoCoronal
$DoSagittal = To-Bool $DoSagittal
$Recurse    = To-Bool $Recurse

function Test-Tool([string]$path) {
  if (-not (Test-Path $path)) { throw "c3d nicht gefunden: $path" }
  $null = & $path -version 2>$null
  if ($LASTEXITCODE -ne 0) { throw "c3d ist nicht ausführbar: $path" }
}

function Get-Stem([string]$fullPath) {
  $name = [IO.Path]::GetFileName($fullPath)
  if ($name.ToLower().EndsWith(".nii.gz")) { return $name.Substring(0, $name.Length - 7) }
  return [IO.Path]::GetFileNameWithoutExtension($name)
}

function Get-Dims([string]$c3d, [string]$file) {
  # Beispielausgabe c3d -info:
  # Image #1: dim = [256, 256, 150]; bb = {...}
  $info = & $c3d $file -info 2>&1

  # Inhalt in den eckigen Klammern nach "dim ="
  $m = [regex]::Match($info, 'dim\s*=\s*\[\s*([^\]]+)\]')
  if ($m.Success) {
    # Alle Ganzzahlen extrahieren, egal ob per Komma oder Leerzeichen getrennt
    $nums = [regex]::Matches($m.Groups[1].Value, '\d+')
    if ($nums.Count -ge 3) {
      return [int]$nums[0].Value, [int]$nums[1].Value, [int]$nums[2].Value
    }
  }
  throw "Konnte Dimensionen nicht auslesen für: $file`n$info"
}

function Get-Indices([int]$n, [int]$frames) {
  if ($n -lt 1) { return @(0) }
  $count = [math]::Min([math]::Max($frames,1), $n)
  if ($count -le 1) { return @(0) }
  $seen = @{}
  $out = New-Object System.Collections.Generic.List[int]
  for ($i = 0; $i -lt $count; $i++) {
    $k = [int][math]::Round($i * ($n - 1) / [double]($count - 1))
    if (-not $seen.ContainsKey($k)) { $seen[$k] = $true; $out.Add($k) }
  }
  return $out
}

# --- Hauptlogik ---
Test-Tool $C3DPath
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$gciParams = @{ Path = $InputRoot; File = $true }
if ($Recurse) { $gciParams.Recurse = $true }

$files = Get-ChildItem @gciParams | Where-Object { $_.Name -match '\.nii(\.gz)?$' }
if (-not $files) {
  Write-Host "Keine NIfTI-Dateien gefunden unter: $InputRoot"
  exit 0
}

foreach ($f in $files) {
  $stem    = Get-Stem $f.FullName
  $baseOut = Join-Path $OutputRoot $stem

  $axDir = Join-Path $baseOut "axial"
  $coDir = Join-Path $baseOut "coronal"
  $saDir = Join-Path $baseOut "sagittal"

  if ($DoAxial)   { New-Item -ItemType Directory -Force -Path $axDir | Out-Null }
  if ($DoCoronal) { New-Item -ItemType Directory -Force -Path $coDir | Out-Null }
  if ($DoSagittal){ New-Item -ItemType Directory -Force -Path $saDir | Out-Null }

  $nx,$ny,$nz = Get-Dims $C3DPath $f.FullName

  if ($DoAxial) {
    $idx = Get-Indices $nz $Frames
    $i = 0
    foreach ($k in $idx) {
      $outPng = Join-Path $axDir ("frame_{0:d3}.png" -f $i)
      & $C3DPath $f.FullName `
        -stretch "$LowPercent%" "$HighPercent%" 0 255 -type uchar `
        -slice z $k -o $outPng
      if ($LASTEXITCODE -ne 0) { throw "c3d Fehler (axial) bei '$($f.FullName)', Slice $k" }
      $i++
    }
  }

  if ($DoCoronal) {
    $idx = Get-Indices $ny $Frames
    $i = 0
    foreach ($k in $idx) {
      $outPng = Join-Path $coDir ("frame_{0:d3}.png" -f $i)
      & $C3DPath $f.FullName `
        -stretch "$LowPercent%" "$HighPercent%" 0 255 -type uchar `
        -slice y $k -o $outPng
      if ($LASTEXITCODE -ne 0) { throw "c3d Fehler (coronal) bei '$($f.FullName)', Slice $k" }
      $i++
    }
  }

  if ($DoSagittal) {
    $idx = Get-Indices $nx $Frames
    $i = 0
    foreach ($k in $idx) {
      $outPng = Join-Path $saDir ("frame_{0:d3}.png" -f $i)
      & $C3DPath $f.FullName `
        -stretch "$LowPercent%" "$HighPercent%" 0 255 -type uchar `
        -slice x $k -o $outPng
      if ($LASTEXITCODE -ne 0) { throw "c3d Fehler (sagittal) bei '$($f.FullName)', Slice $k" }
      $i++
    }
  }
}

Write-Host "Fertig. Ausgaben liegen unter: $OutputRoot"

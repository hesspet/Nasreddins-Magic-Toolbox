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

  [ValidateRange(64,8192)]
  [int]$LongEdge = 1280,                            # Zielgröße: lange Kante in Pixel

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

# Liest Dimensionen (nx,ny,nz) UND Voxelabstände (sx,sy,sz) aus c3d -info
function Get-DimsVox([string]$c3d, [string]$file) {
  $info = & $c3d $file -info 2>&1

  $mDim = [regex]::Match($info, 'dim\s*=\s*\[\s*([^\]]+)\]')
  if (-not $mDim.Success) { throw "Konnte Dimensionen nicht auslesen für: $file`n$info" }
  $dims = [regex]::Matches($mDim.Groups[1].Value, '\d+')
  if ($dims.Count -lt 3)   { throw "Konnte Dimensionen nicht auslesen für: $file`n$info" }
  $nx = [int]$dims[0].Value; $ny = [int]$dims[1].Value; $nz = [int]$dims[2].Value

  $mVox = [regex]::Match($info, 'vox\s*=\s*\[\s*([^\]]+)\]')
  if (-not $mVox.Success) { throw "Konnte Voxelabstände nicht auslesen für: $file`n$info" }
  $vox = [regex]::Matches($mVox.Groups[1].Value, '[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?')
  if ($vox.Count -lt 3)    { throw "Konnte Voxelabstände nicht auslesen für: $file`n$info" }
  $sx = [double]$vox[0].Value; $sy = [double]$vox[1].Value; $sz = [double]$vox[2].Value

  return @($nx,$ny,$nz,$sx,$sy,$sz)
}

# Berechnet Zielgröße (Breite,Höhe) in Pixel pro Ebene bei gegebener LongEdge; hält physikalische Proportionen
function Get-PlaneSize([int]$nx,[int]$ny,[int]$nz,[double]$sx,[double]$sy,[double]$sz,[char]$plane,[int]$longEdge) {
  switch ($plane) {
    'z' { $pw = $nx * $sx; $ph = $ny * $sy } # axial: x vs y
    'y' { $pw = $nx * $sx; $ph = $nz * $sz } # koronal: x vs z
    'x' { $pw = $ny * $sy; $ph = $nz * $sz } # sagittal: y vs z
    default { throw "Unbekannte Ebene: $plane" }
  }
  if ($pw -le 0 -or $ph -le 0) { return @($longEdge, $longEdge) }
  $ratio = $pw / $ph
  if ($ratio -ge 1) {
    $w = $longEdge
    $h = [int][math]::Max(1, [math]::Round($longEdge / $ratio))
  } else {
    $h = $longEdge
    $w = [int][math]::Max(1, [math]::Round($longEdge * $ratio))
  }
  return @($w, $h)
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

  $nx,$ny,$nz,$sx,$sy,$sz = Get-DimsVox $C3DPath $f.FullName

  # Zielgrößen je Ebene (einmal pro Volumen berechnen)
  if ($DoAxial)   { $axW,$axH = Get-PlaneSize $nx $ny $nz $sx $sy $sz 'z' $LongEdge }
  if ($DoCoronal) { $coW,$coH = Get-PlaneSize $nx $ny $nz $sx $sy $sz 'y' $LongEdge }
  if ($DoSagittal){ $saW,$saH = Get-PlaneSize $nx $ny $nz $sx $sy $sz 'x' $LongEdge }

  # Indizes pro Achse
  if ($DoAxial)   { $idxZ = Get-Indices $nz $Frames }
  if ($DoCoronal) { $idxY = Get-Indices $ny $Frames }
  if ($DoSagittal){ $idxX = Get-Indices $nx $Frames }

  # Hinweis: -stretch/-type VOR dem Slicen => konsistente Helligkeit über alle Frames
  if ($DoAxial) {
    $i = 0
    foreach ($k in $idxZ) {
      $outPng = Join-Path $axDir ("frame_{0:d3}.png" -f $i)
      & $C3DPath $f.FullName `
        -stretch "$LowPercent%" "$HighPercent%" 0 255 -type uchar `
        -slice z $k -interpolation Cubic -resample "${axW}x${axH}x1" `
        -o $outPng
      if ($LASTEXITCODE -ne 0) { throw "c3d Fehler (axial) bei '$($f.FullName)', Slice $k" }
      $i++
    }
  }

  if ($DoCoronal) {
    $i = 0
    foreach ($k in $idxY) {
      $outPng = Join-Path $coDir ("frame_{0:d3}.png" -f $i)
      & $C3DPath $f.FullName `
        -stretch "$LowPercent%" "$HighPercent%" 0 255 -type uchar `
        -slice y $k -interpolation Cubic -resample "${coW}x${coH}x1" `
        -o $outPng
      if ($LASTEXITCODE -ne 0) { throw "c3d Fehler (coronal) bei '$($f.FullName)', Slice $k" }
      $i++
    }
  }

  if ($DoSagittal) {
    $i = 0
    foreach ($k in $idxX) {
      $outPng = Join-Path $saDir ("frame_{0:d3}.png" -f $i)
      & $C3DPath $f.FullName `
        -stretch "$LowPercent%" "$HighPercent%" 0 255 -type uchar `
        -slice x $k -interpolation Cubic -resample "${saW}x${saH}x1" `
        -o $outPng
      if ($LASTEXITCODE -ne 0) { throw "c3d Fehler (sagittal) bei '$($f.FullName)', Slice $k" }
      $i++
    }
  }
}

Write-Host "Fertig. Ausgaben liegen unter: $OutputRoot"

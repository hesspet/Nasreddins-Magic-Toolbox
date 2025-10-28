# Pfade anpassen
$c3d  = "C:\Program Files (x86)\Convert3D\bin\c3d.exe"
$in   = "C:\Users\hesspet\Downloads\IXI-T1\IXI611-HH-2650-T1.nii.gz"
$ax   = "C:\out\axial"
$cor  = "C:\out\coronal"
New-Item -ItemType Directory -Force -Path $ax,$cor | Out-Null

# Volumengröße auslesen
$info = & $c3d $in -info
if ($info -match 'dim = \[(\d+)\s+(\d+)\s+(\d+)\]'){ $nx=$matches[1]; $ny=$matches[2]; $nz=$matches[3] }

# 50 axial (z-Richtung)
0..49 | % {
  $k = [int][math]::Round($_ * ($nz-1)/49)
  & $c3d $in -pim -stretch 2% 98% 0 255 -type uchar -slice z $k -o (Join-Path $ax ("frame_{0:D3}.png" -f $_))
}

# 50 koronal (y-Richtung)  → seitliche Ansicht gewünscht? nimm stattdessen -slice x
0..49 | % {
  $k = [int][math]::Round($_ * ($ny-1)/49)
  & $c3d $in -pim -stretch 2% 98% 0 255 -type uchar -slice y $k -o (Join-Path $cor ("frame_{0:D3}.png" -f $_))
}

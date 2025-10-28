@echo off
REM --- KONFIG ---
set "C3D=C:\Program Files (x86)\Convert3D\bin\c3d.exe"
set "IN=C:\Users\hesspet\Downloads\IXI-T1"
set "OUT=C:\Users\hesspet\Downloads\IXI-T1-PNG"
set "FRAMES=50"
set "LOW=2"
set "HIGH=98"

REM Booleans: 1 oder 0 (KEIN true/false!)
set "DOAXIAL=1"
set "DOCORONAL=1"
set "DOSAGITTAL=0"
set "RECURSE=1"
REM -------------

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0convert_slices.ps1" ^
  -C3DPath "%C3D%" ^
  -InputRoot "%IN%" ^
  -OutputRoot "%OUT%" ^
  -Frames %FRAMES% -LowPercent %LOW% -HighPercent %HIGH% ^
  -DoAxial %DOAXIAL% -DoCoronal %DOCORONAL% -DoSagittal %DOSAGITTAL% -Recurse %RECURSE%

pause

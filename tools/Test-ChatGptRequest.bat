@echo off
setlocal enabledelayedexpansion

if "%~1"=="" (
    echo Nutzungsbeispiel: %~nx0 API_KEY [CHATGPT_URL] [TESTNACHRICHT]
    echo.
    echo - API_KEY:        Gültiger API-Schluessel fuer den ChatGPT-Endpunkt.
    echo - CHATGPT_URL:    (Optional) Vollstaendige URL fuer den Chat-Completions-Endpunkt.
    echo - TESTNACHRICHT:  (Optional) Benutzerdefinierter Prompt. Standard: "Hallo".
    exit /b 1
)

set "API_KEY=%~1"
shift
set "CHATGPT_URL=%~1"
if "%CHATGPT_URL%"=="" (
    set "CHATGPT_URL=https://api.openai.com/v1/chat/completions"
)
shift
set "PROMPT=%*"
if not defined PROMPT (
    set "PROMPT=Hallo"
)

powershell -NoProfile -Command ^
    "param([string]$ApiKey,[string]$Url,[string]$Prompt);" ^
    "$ErrorActionPreference = 'Stop';" ^
    "try {" ^
    "    if ([string]::IsNullOrWhiteSpace($Url)) { $Url = 'https://api.openai.com/v1/chat/completions' }" ^
    "    if ([string]::IsNullOrWhiteSpace($Prompt)) { $Prompt = 'Hallo' }" ^
    "    $headers = @{ 'Content-Type' = 'application/json' };" ^
    "    if (-not [string]::IsNullOrWhiteSpace($ApiKey)) { $headers['Authorization'] = 'Bearer ' + $ApiKey }" ^
    "    $payload = @{ model = 'gpt-4o-mini'; messages = @(@{ role = 'user'; content = $Prompt }) } | ConvertTo-Json -Depth 5;" ^
    "    Write-Host 'Sende Testanfrage an ' -NoNewline; Write-Host $Url -ForegroundColor Cyan;" ^
    "    Write-Host 'Testnachricht: ' -NoNewline; Write-Host $Prompt -ForegroundColor Yellow;" ^
    "    $response = Invoke-RestMethod -Method Post -Uri $Url -Headers $headers -Body $payload;" ^
    "    Write-Host 'Antwort erhalten:' -ForegroundColor Green;" ^
    "    $response | ConvertTo-Json -Depth 10;" ^
    "} catch {" ^
    "    Write-Host 'Fehler bei der Testanfrage:' -ForegroundColor Red;" ^
    "    Write-Host $_.Exception.Message -ForegroundColor Red;" ^
    "    if ($_.Exception.Response) {" ^
    "        try {" ^
    "            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream());" ^
    "            $content = $reader.ReadToEnd();" ^
    "            if ($content) {" ^
    "                Write-Host 'Fehlerantwort:' -ForegroundColor Yellow;" ^
    "                Write-Host $content;" ^
    "            }" ^
    "        } catch { Write-Verbose 'Fehler beim Lesen der Fehlerantwort.' }" ^
    "    }" ^
    "    exit 1;" ^
    "}" ^
    "%API_KEY%" "%CHATGPT_URL%" "%PROMPT%"

endlocal

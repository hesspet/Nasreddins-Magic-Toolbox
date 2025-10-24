<#!
.SYNOPSIS
    Sendet eine HTTP-Anfrage an eine API und gibt die Antwort direkt in der Konsole aus.

.DESCRIPTION
    Dieses Skript dient dazu, eine API schnell mit unterschiedlichen Zugangsdaten,
    HTTP-Methoden, Headern und Anfragekörpern zu testen. Tragen Sie Ihre individuellen
    Werte in die Parameter ein (z.B. direkt beim Aufruf oder indem Sie die Standardwerte
    unten anpassen) und führen Sie das Skript aus. Die Antwort der API wird im Terminal
    angezeigt.

.EXAMPLE
    # Beispiel-Aufruf mit GET-Anfrage
    .\\Test-ApiRequest.ps1 -BaseUri "https://api.example.com" -Endpoint "/status" -Username "user" -Password "secret"

.EXAMPLE
    # Beispiel-Aufruf mit POST-Anfrage und JSON-Body
    .\\Test-ApiRequest.ps1 -BaseUri "https://api.example.com" -Endpoint "/login" -Method POST `
        -Username "user" -Password "secret" -Body @{ email = "user@example.com"; password = "secret" }

.NOTES
    * Setzen Sie -SkipCertificateCheck, wenn Sie gegen eine API mit selbstsigniertem Zertifikat testen.
    * Wenn Sie zusätzliche Header benötigen (z.B. API-Key), können Sie diese über -Headers @{ "x-api-key" = "..." } hinzufügen.
    * Für andere Authentifizierungsverfahren können Sie die Header manuell setzen und -Username/-Password weglassen.
#>

[CmdletBinding()]
param (
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$BaseUri = "https://api.example.com",

    [Parameter()]
    [ValidateNotNull()]
    [string]$Endpoint = "/status",

    [Parameter()]
    [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")]
    [string]$Method = "GET",

    [Parameter()]
    [string]$Username = "meinBenutzername",

    [Parameter()]
    [string]$Password = "meinPasswort",

    [Parameter()]
    [Hashtable]$Headers = @{},

    [Parameter()]
    [object]$Body,

    [switch]$SkipCertificateCheck,

    [switch]$AsRaw
)

function New-RequestBodyJson {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [string]) {
        return $Value
    }

    try {
        return ($Value | ConvertTo-Json -Depth 10)
    }
    catch {
        Write-Warning "Der angegebene Body konnte nicht in JSON konvertiert werden. Es wird versucht, ihn unverändert zu senden."
        return $Value
    }
}

try {
    $uriBuilder = [System.UriBuilder]::new($BaseUri)
    $uriBuilder.Path = [System.IO.Path]::Combine($uriBuilder.Path.TrimEnd('/'), $Endpoint.TrimStart('/'))
    $uri = $uriBuilder.Uri.AbsoluteUri
}
catch {
    throw "Die angegebene Kombination aus BaseUri und Endpoint ist keine gültige URL: $($_.Exception.Message)"
}

$invokeParams = @{
    Uri         = $uri
    Method      = $Method
    ErrorAction = 'Stop'
}

if ($Username -and $Password) {
    $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential ($Username, $securePassword)
    $invokeParams.Credential = $credential
}

$defaultHeaders = @{
    'Accept' = 'application/json'
}

if ($Headers) {
    foreach ($key in $Headers.Keys) {
        $defaultHeaders[$key] = $Headers[$key]
    }
}

$invokeParams.Headers = $defaultHeaders

$bodyJson = New-RequestBodyJson -Value $Body
if ($null -ne $bodyJson -and $Method -in @('POST', 'PUT', 'PATCH', 'DELETE')) {
    $invokeParams.Body = $bodyJson
    if (-not $invokeParams.Headers.ContainsKey('Content-Type')) {
        $invokeParams.Headers['Content-Type'] = 'application/json'
    }
}

if ($SkipCertificateCheck) {
    add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem) {
        return true;
    }
}
"@ -ErrorAction SilentlyContinue
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
}

Write-Host "\nSende $Method-Anfrage an: $uri" -ForegroundColor Cyan
if ($invokeParams.ContainsKey('Body')) {
    Write-Host "Request-Body:" -ForegroundColor DarkCyan
    if ($invokeParams.Body -is [string]) {
        Write-Host $invokeParams.Body
    } else {
        $invokeParams.Body | Format-List | Out-String | Write-Host
    }
}

try {
    if ($AsRaw) {
        $response = Invoke-WebRequest @invokeParams
        Write-Host "\nStatuscode: $($response.StatusCode) $($response.StatusDescription)" -ForegroundColor Green
        Write-Host "Antwort (Raw):" -ForegroundColor DarkGreen
        $response.Content
    }
    else {
        $response = Invoke-RestMethod @invokeParams
        Write-Host "\nAntwort:" -ForegroundColor DarkGreen
        $response | ConvertTo-Json -Depth 10
    }
}
catch {
    Write-Host "\nBeim Aufruf der API ist ein Fehler aufgetreten:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorContent = $reader.ReadToEnd()
            if ($errorContent) {
                Write-Host "\nFehlerantwort des Servers:" -ForegroundColor Yellow
                Write-Host $errorContent
            }
        }
        catch {
            Write-Verbose "Fehler beim Lesen des Fehlerstreams: $($_.Exception.Message)"
        }
    }
    exit 1
}

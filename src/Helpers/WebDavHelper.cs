using System;
using System.IO;
using System.Net;
using System.Text;
using WebDav;

namespace Toolbox.Helpers;

/// <summary>
/// Provides helper methods to interact with WebDAV endpoints from the browser.
/// </summary>
public static class WebDavHelper
{
    private const string TestFilePrefix = "toolbox-connection-test-";

    /// <summary>
    /// Tries to establish a WebDAV connection by creating and deleting a temporary file.
    /// </summary>
    public static async Task<WebDavTestResult> TestConnectionAsync(string url, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return WebDavTestResult.Failure("Bitte geben Sie eine WebDAV-URL an.");
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return WebDavTestResult.Failure("Die WebDAV-URL ist ungültig.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return WebDavTestResult.Failure("Die WebDAV-URL muss mit http oder https beginnen.");
        }

        var clientParams = new WebDavClientParams
        {
            BaseAddress = uri,
            Credentials = string.IsNullOrWhiteSpace(username)
                ? null
                : new NetworkCredential(username.Trim(), password ?? string.Empty)
        };

        var client = new WebDavClient(clientParams);
        var remoteFileName = $"/{TestFilePrefix}{Guid.NewGuid():N}.txt";

        try
        {
            using var payload = new MemoryStream(Encoding.UTF8.GetBytes(CreateTestFileContent()));

            var putResponse = await client.PutFile(remoteFileName, payload, "text/plain");
            if (!putResponse.IsSuccessful)
            {
                return WebDavTestResult.Failure($"Die Testdatei konnte nicht erstellt werden: {DescribeResponse(putResponse)}");
            }

            var deleteResponse = await client.Delete(remoteFileName);
            if (!deleteResponse.IsSuccessful)
            {
                return WebDavTestResult.Failure($"Die Testdatei konnte nicht gelöscht werden. Bitte entfernen Sie sie manuell. Details: {DescribeResponse(deleteResponse)}");
            }

            return WebDavTestResult.Success("Die Verbindung zum WebDAV-Server wurde erfolgreich getestet.");
        }
        catch (UriFormatException)
        {
            return WebDavTestResult.Failure("Die WebDAV-URL ist ungültig.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            return WebDavTestResult.Failure($"Die Verbindung konnte nicht aufgebaut werden. Häufig blockiert der Browser Anfragen ohne passende CORS-Header. Technische Details: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            var statusCode = (int)ex.StatusCode!.Value;
            return WebDavTestResult.Failure($"Die Verbindung konnte nicht aufgebaut werden. Der Server antwortete mit Statuscode {statusCode} ({ex.StatusCode}). {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return WebDavTestResult.Failure("Die Verbindung wurde wegen eines Zeitüberschreitungsfehlers abgebrochen.");
        }
        catch (NotSupportedException ex)
        {
            return WebDavTestResult.Failure($"Die Anfrage wird vom Browser nicht unterstützt: {ex.Message}");
        }
        catch (Exception ex)
        {
            return WebDavTestResult.Failure($"Beim Testen der Verbindung ist ein unbekannter Fehler aufgetreten: {ex.Message}");
        }
    }

    private static string CreateTestFileContent()
    {
        return $"Nasreddins Magic Toolbox WebDAV-Verbindungstest {DateTime.UtcNow:O}";
    }

    private static string DescribeResponse(WebDavResponse response)
    {
        var description = string.IsNullOrWhiteSpace(response.Description)
            ? string.Empty
            : $" {response.Description}";

        return $"Statuscode {response.StatusCode}.{description}".Trim();
    }
}

/// <summary>
/// Represents the result of a WebDAV connectivity test.
/// </summary>
public sealed record WebDavTestResult(bool IsSuccessful, string Message)
{
    public static WebDavTestResult Success(string message) => new(true, message);

    public static WebDavTestResult Failure(string message) => new(false, message);
}

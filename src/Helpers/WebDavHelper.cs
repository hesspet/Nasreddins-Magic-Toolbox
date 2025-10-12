using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

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

        var remoteFileUri = BuildRemoteFileUri(uri);

        using var client = new HttpClient();

        if (!string.IsNullOrWhiteSpace(username))
        {
            var sanitizedUsername = username.Trim();
            var credentialBytes = Encoding.UTF8.GetBytes($"{sanitizedUsername}:{password ?? string.Empty}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(credentialBytes));
        }

        try
        {
            using var payload = new StringContent(CreateTestFileContent(), Encoding.UTF8, "text/plain");

            using var putResponse = await client.PutAsync(remoteFileUri, payload);
            if (!putResponse.IsSuccessStatusCode)
            {
                return WebDavTestResult.Failure($"Die Testdatei konnte nicht erstellt werden: {DescribeResponse(putResponse)}");
            }

            using var deleteResponse = await client.DeleteAsync(remoteFileUri);
            if (!deleteResponse.IsSuccessStatusCode)
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

    private static Uri BuildRemoteFileUri(Uri baseUri)
    {
        var baseUriString = baseUri.GetLeftPart(UriPartial.Authority) + baseUri.AbsolutePath;
        if (!baseUriString.EndsWith('/'))
        {
            baseUriString += '/';
        }

        var normalizedBaseUri = new Uri(baseUriString);
        var remoteFileName = $"{TestFilePrefix}{Guid.NewGuid():N}.txt";
        return new Uri(normalizedBaseUri, remoteFileName);
    }

    private static string DescribeResponse(HttpResponseMessage response)
    {
        var description = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? string.Empty
            : $" {response.ReasonPhrase}";

        return $"Statuscode {(int)response.StatusCode} ({response.StatusCode}).{description}".Trim();
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

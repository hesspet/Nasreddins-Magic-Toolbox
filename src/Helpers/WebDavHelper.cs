using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

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

        using var client = CreateHttpClient();

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

    private static HttpClient CreateHttpClient()
    {
#if NET8_0_OR_GREATER
        var handler = new WebAssemblyHttpHandler();
        ConfigureBrowserRequestOptions(handler);

        return new HttpClient(handler);
#else
        return new HttpClient();
#endif
    }

    private static void ConfigureBrowserRequestOptions(WebAssemblyHttpHandler handler)
    {
        if (handler is null)
        {
            return;
        }

        if (!TryConfigureOptionsObject(handler))
        {
            TrySetEnumProperty(handler, "DefaultBrowserRequestMode", "Cors");
            TrySetEnumProperty(handler, "DefaultBrowserRequestCache", "NoStore");
            TrySetEnumProperty(handler, "DefaultBrowserRequestCredentials", "Include");
        }
    }

    private static bool TryConfigureOptionsObject(WebAssemblyHttpHandler handler)
    {
        var optionsProperty = handler.GetType().GetProperty("DefaultBrowserRequestOptions", BindingFlags.Instance | BindingFlags.Public);
        if (optionsProperty is null)
        {
            return false;
        }

        var optionsInstance = optionsProperty.GetValue(handler);
        if (optionsInstance is null)
        {
            optionsInstance = CreateInstance(optionsProperty.PropertyType);
            if (optionsInstance is null)
            {
                return false;
            }

            if (optionsProperty.CanWrite)
            {
                optionsProperty.SetValue(handler, optionsInstance);
            }
            else
            {
                return false;
            }
        }

        if (optionsInstance is null)
        {
            return false;
        }

        var configured = false;
        configured |= TrySetEnumProperty(optionsInstance, "Mode", "Cors");
        configured |= TrySetEnumProperty(optionsInstance, "Cache", "NoStore");
        configured |= TrySetEnumProperty(optionsInstance, "Credentials", "Include");

        if (!configured)
        {
            return false;
        }

        return true;
    }

    private static object? CreateInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetEnumProperty(object target, string propertyName, string enumValueName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property is null || !property.CanWrite)
        {
            return false;
        }

        object? value = null;

        if (property.PropertyType.IsEnum)
        {
            try
            {
                value = Enum.Parse(property.PropertyType, enumValueName, ignoreCase: true);
            }
            catch
            {
                return false;
            }
        }
        else if (property.PropertyType == typeof(string))
        {
            value = enumValueName;
        }
        else
        {
            return false;
        }

        property.SetValue(target, value);
        return true;
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

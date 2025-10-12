using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Toolbox.Helpers;

/// <summary>
/// Provides helper methods for interacting with a WebDAV server.
/// </summary>
public class WebDavHelper
{
    private const string TestFileContent = "WebDAV connectivity test";
    private const string PropfindRequestBody = "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\"><propname /></propfind>";

    /// <summary>
    /// Validates that the supplied WebDAV credentials are functional by creating and deleting a test file.
    /// </summary>
    public async Task TestConnectionAsync(string url, string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var sanitizedUrl = url.Trim();

        var baseAddress = CreateBaseAddress(sanitizedUrl);
        using var client = CreateHttpClient(baseAddress);

        var authorizationHeader = CreateAuthorizationHeader(username, password);

        await EnsureSuccessfulAsync(
            ct => SendPropfindAsync(client, baseAddress, authorizationHeader, ct),
            "Anmeldung",
            cancellationToken);

        var fileName = $"test{DateTimeOffset.Now:yyyyMMddHHmmss}.txt";
        var fileUri = new Uri(baseAddress, fileName);
        var fileContent = Encoding.UTF8.GetBytes(TestFileContent);

        await EnsureSuccessfulAsync(
            ct => PutFileAsync(client, fileUri, authorizationHeader, fileContent, ct),
            "Datei anlegen",
            cancellationToken);

        await EnsureSuccessfulAsync(
            ct => DeleteAsync(client, fileUri, authorizationHeader, ct),
            "Datei löschen",
            cancellationToken);
    }

    private static Uri CreateBaseAddress(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        throw new ArgumentException("Die angegebene WebDAV-URL ist ungültig.", nameof(url));
    }

    private static async Task EnsureSuccessfulAsync(Func<CancellationToken, Task<HttpResponseMessage>> operation, string operationName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HttpResponseMessage response;

        try
        {
            response = await operation(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "Die Verbindung zum WebDAV-Server konnte nicht hergestellt werden. Bitte überprüfe die angegebene URL und deine Netzwerkverbindung.",
                ex);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var statusDescription = response.ReasonPhrase;
            var message = string.IsNullOrWhiteSpace(statusDescription)
                ? $"{operationName} fehlgeschlagen (Statuscode {(int)response.StatusCode})."
                : $"{operationName} fehlgeschlagen: {statusDescription}";

            throw new InvalidOperationException(message);
        }
    }

    private static HttpClient CreateHttpClient(Uri baseAddress)
    {
        var client = new HttpClient
        {
            BaseAddress = baseAddress
        };

        return client;
    }

    private static AuthenticationHeaderValue? CreateAuthorizationHeader(string username, string password)
    {
        if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
        {
            return null;
        }

        var credentialBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
    }

    private static async Task<HttpResponseMessage> SendPropfindAsync(HttpClient client, Uri requestUri, AuthenticationHeaderValue? authorizationHeader, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestUri)
        {
            Content = new StringContent(PropfindRequestBody, Encoding.UTF8, "application/xml")
        };

        request.Headers.Add("Depth", "0");

        if (authorizationHeader is not null)
        {
            request.Headers.Authorization = authorizationHeader;
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> PutFileAsync(HttpClient client, Uri requestUri, AuthenticationHeaderValue? authorizationHeader, byte[] content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = new ByteArrayContent(content)
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        if (authorizationHeader is not null)
        {
            request.Headers.Authorization = authorizationHeader;
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, Uri requestUri, AuthenticationHeaderValue? authorizationHeader, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

        if (authorizationHeader is not null)
        {
            request.Headers.Authorization = authorizationHeader;
        }

        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

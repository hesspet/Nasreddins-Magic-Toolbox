using System.IO;
using System.Net;
using System.Text;
using WebDav;

namespace Toolbox.Helpers;

/// <summary>
/// Provides helper methods for interacting with a WebDAV server.
/// </summary>
public class WebDavHelper
{
    private const string TestFileContent = "WebDAV connectivity test";

    /// <summary>
    /// Validates that the supplied WebDAV credentials are functional by creating and deleting a test file.
    /// </summary>
    public async Task TestConnectionAsync(string url, string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var sanitizedUrl = url.Trim();

        var clientParams = new WebDavClientParams
        {
            BaseAddress = CreateBaseAddress(sanitizedUrl),
            Credentials = new NetworkCredential(username ?? string.Empty, password ?? string.Empty)
        };

        using var client = new WebDavClient(clientParams);

        await EnsureSuccessfulAsync(() => client.Propfind(string.Empty), "Anmeldung", cancellationToken);

        var fileName = $"test{DateTimeOffset.Now:yyyyMMddHHmmss}.txt";

        using (var contentStream = new MemoryStream(Encoding.UTF8.GetBytes(TestFileContent)))
        {
            await EnsureSuccessfulAsync(() => client.PutFile(fileName, contentStream, "text/plain"), "Datei anlegen", cancellationToken);
        }

        await EnsureSuccessfulAsync(() => client.Delete(fileName), "Datei löschen", cancellationToken);
    }

    private static Uri CreateBaseAddress(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        throw new ArgumentException("Die angegebene WebDAV-URL ist ungültig.", nameof(url));
    }

    private static async Task EnsureSuccessfulAsync(Func<Task<WebDavResponse>> operation, string operationName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await operation().ConfigureAwait(false);

        if (response.IsSuccessful)
        {
            return;
        }

        var statusDescription = response.Description;
        var message = string.IsNullOrWhiteSpace(statusDescription)
            ? $"{operationName} fehlgeschlagen (Statuscode {(int)response.StatusCode})."
            : $"{operationName} fehlgeschlagen: {statusDescription}";

        throw new InvalidOperationException(message);
    }
}

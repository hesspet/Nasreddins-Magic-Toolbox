using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;

namespace Toolbox.Helpers;

/// <summary>
///     Stellt Hilfsfunktionen bereit, um Bilder aus dem Browser-Upload entgegenzunehmen und in ein
///     einheitliches Format zu überführen. Die Klasse kapselt die komplette Stream-Verarbeitung und
///     liefert das Ergebnis samt MIME-Typ zurück.
/// </summary>
public static class ImageProcessingHelper
{
    /// <summary>
    ///     Liest die vom Browser bereitgestellte Datei vollständig ein, achtet dabei auf die maximal
    ///     erlaubte Dateigröße und liefert die rohen Bytes inklusive Content-Type zurück. Eine
    ///     nachgelagerte Skalierung kann mit den Rohdaten erfolgen.
    /// </summary>
    public static async Task<ProcessedImage> LoadResizedImageAsync(
        IBrowserFile file,
        long maxFileSize,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        await using var readStream = file.OpenReadStream(maxFileSize);
        using var copyStream = new MemoryStream();
        await readStream.CopyToAsync(copyStream, cancellationToken).ConfigureAwait(false);

        var contentType = !string.IsNullOrWhiteSpace(file.ContentType)
            ? file.ContentType
            : "application/octet-stream";

        return new ProcessedImage(copyStream.ToArray(), contentType);
    }

    /// <summary>
    ///     Ergebniscontainer, der sowohl die Bilddaten als auch den erkannten MIME-Typ transportiert.
    /// </summary>
    public readonly record struct ProcessedImage(byte[] Data, string ContentType);
}

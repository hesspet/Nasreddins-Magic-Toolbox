using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;

namespace Toolbox.Helpers;

public static class ImageProcessingHelper
{
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

    public readonly record struct ProcessedImage(byte[] Data, string ContentType);
}

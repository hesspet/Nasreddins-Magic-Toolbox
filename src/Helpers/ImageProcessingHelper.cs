using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Toolbox.Helpers;

public static class ImageProcessingHelper
{
    public static async Task<ProcessedImage> LoadResizedImageAsync(
        IBrowserFile file,
        long maxFileSize,
        int maxDimension,
        CancellationToken cancellationToken = default)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        await using var readStream = file.OpenReadStream(maxFileSize);
        return await ReadAndResizeAsync(readStream, file.ContentType, maxDimension, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ProcessedImage> ReadAndResizeAsync(
        Stream sourceStream,
        string? contentType,
        int maxDimension,
        CancellationToken cancellationToken = default)
    {
        if (sourceStream is null)
        {
            throw new ArgumentNullException(nameof(sourceStream));
        }

        if (maxDimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDimension));
        }

        Stream workingStream = sourceStream;
        MemoryStream? bufferedStream = null;

        if (!sourceStream.CanSeek)
        {
            bufferedStream = new MemoryStream();
            await sourceStream.CopyToAsync(bufferedStream, cancellationToken).ConfigureAwait(false);
            bufferedStream.Position = 0;
            workingStream = bufferedStream;
        }
        else if (sourceStream.Position != 0)
        {
            sourceStream.Position = 0;
        }

        var decoderOptions = new DecoderOptions
        {
            Configuration = Configuration.Default
        };

        var imageInfo = Image.Identify(decoderOptions, workingStream);
        if (imageInfo is null)
        {
            throw new InvalidDataException("Die ausgewählte Datei ist kein unterstütztes Bildformat.");
        }

        var imageFormat = imageInfo.Metadata.DecodedImageFormat;
        if (imageFormat is null)
        {
            throw new InvalidDataException("Die ausgewählte Datei ist kein unterstütztes Bildformat.");
        }

        var longestSide = Math.Max(imageInfo.Width, imageInfo.Height);
        var mimeType = !string.IsNullOrWhiteSpace(contentType)
            ? contentType
            : imageFormat.DefaultMimeType;

        if (workingStream.CanSeek)
        {
            workingStream.Position = 0;
        }

        if (longestSide <= maxDimension)
        {
            if (bufferedStream is not null)
            {
                return new ProcessedImage(bufferedStream.ToArray(), mimeType);
            }

            using var copyStream = new MemoryStream();
            await workingStream.CopyToAsync(copyStream, cancellationToken).ConfigureAwait(false);
            return new ProcessedImage(copyStream.ToArray(), mimeType);
        }

        using var image = await Image.LoadAsync<Rgba32>(decoderOptions, workingStream, cancellationToken).ConfigureAwait(false);

        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(maxDimension, maxDimension),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Bicubic
        }));

        var encoder = GetEncoderForImageFormat(imageFormat) ?? new PngEncoder();
        var outputMimeType = GetMimeTypeForEncoder(encoder, mimeType);

        using var outputStream = new MemoryStream();
        await image.SaveAsync(outputStream, encoder, cancellationToken).ConfigureAwait(false);

        return new ProcessedImage(outputStream.ToArray(), outputMimeType);
    }

    private static IImageEncoder? GetEncoderForImageFormat(IImageFormat imageFormat)
        => imageFormat switch
        {
            PngFormat => new PngEncoder(),
            JpegFormat => new JpegEncoder(),
            GifFormat => new GifEncoder(),
            BmpFormat => new BmpEncoder(),
            TgaFormat => new TgaEncoder(),
            WebpFormat => new WebpEncoder(),
            TiffFormat => new TiffEncoder(),
            _ => null
        };

    private static string GetMimeTypeForEncoder(IImageEncoder encoder, string fallbackMimeType)
        => encoder switch
        {
            PngEncoder => "image/png",
            JpegEncoder => "image/jpeg",
            GifEncoder => "image/gif",
            BmpEncoder => "image/bmp",
            TgaEncoder => "image/x-tga",
            WebpEncoder => "image/webp",
            TiffEncoder => "image/tiff",
            _ => fallbackMimeType
        };

    public readonly record struct ProcessedImage(byte[] Data, string ContentType);
}

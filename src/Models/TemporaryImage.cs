using System;

namespace Toolbox.Models;

/// <summary>
///     Represents an image captured by the FotoComponent and stored temporarily for further processing.
/// </summary>
/// <param name="Id">Unique identifier of the stored image.</param>
/// <param name="FileName">Original file name provided by the browser.</param>
/// <param name="ContentType">Content type of the image data.</param>
/// <param name="Data">The raw image data.</param>
/// <param name="CapturedAt">Timestamp when the image was stored.</param>
public sealed record TemporaryImage(
    Guid Id,
    string? FileName,
    string? ContentType,
    byte[] Data,
    DateTimeOffset CapturedAt);

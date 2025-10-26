using System;

namespace Toolbox.Models;

/// <summary>
///     Represents the outcome of a captured photo.
/// </summary>
/// <param name="ImageId">Identifier of the stored image.</param>
/// <param name="FileName">Original file name of the captured image.</param>
/// <param name="ContentType">Content type of the stored image.</param>
/// <param name="Size">Size of the stored image in bytes.</param>
public sealed record PhotoCaptureResult(Guid ImageId, string? FileName, string? ContentType, long Size);

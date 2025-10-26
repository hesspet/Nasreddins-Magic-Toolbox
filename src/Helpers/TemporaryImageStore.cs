using System;
using System.Collections.Generic;
using System.Linq;
using Toolbox.Models;

namespace Toolbox.Helpers;

/// <summary>
///     Provides an in-memory store for temporarily captured images.
/// </summary>
public sealed class TemporaryImageStore
{
    private readonly Dictionary<Guid, TemporaryImage> images = new();
    private readonly object syncRoot = new();

    /// <summary>
    ///     Gets or sets the retention time for stored images.
    ///     Entries older than this duration are automatically removed whenever the store is accessed.
    /// </summary>
    public TimeSpan RetentionDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    ///     Stores the provided image data and returns its identifier.
    /// </summary>
    public Guid StoreImage(string fileName, string contentType, byte[] data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        var entry = new TemporaryImage(Guid.NewGuid(), fileName, contentType, data, DateTimeOffset.UtcNow);

        lock (syncRoot)
        {
            Cleanup_NoLock();
            images[entry.Id] = entry;
        }

        return entry.Id;
    }

    /// <summary>
    ///     Attempts to retrieve the stored image for the specified identifier.
    /// </summary>
    public bool TryGetImage(Guid imageId, out TemporaryImage? image)
    {
        lock (syncRoot)
        {
            Cleanup_NoLock();
            return images.TryGetValue(imageId, out image);
        }
    }

    /// <summary>
    ///     Removes the image with the provided identifier, if it exists.
    /// </summary>
    public bool RemoveImage(Guid imageId)
    {
        lock (syncRoot)
        {
            return images.Remove(imageId);
        }
    }

    /// <summary>
    ///     Removes all stored images.
    /// </summary>
    public void Clear()
    {
        lock (syncRoot)
        {
            images.Clear();
        }
    }

    private void Cleanup_NoLock()
    {
        if (RetentionDuration <= TimeSpan.Zero || images.Count == 0)
        {
            return;
        }

        var threshold = DateTimeOffset.UtcNow - RetentionDuration;
        var expiredIds = images.Values
            .Where(image => image.CapturedAt < threshold)
            .Select(image => image.Id)
            .ToArray();

        if (expiredIds.Length == 0)
        {
            return;
        }

        foreach (var id in expiredIds)
        {
            _ = images.Remove(id);
        }
    }
}

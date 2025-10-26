using System.Collections.Concurrent;

namespace Toolbox.Helpers;

public class TemporaryImageStorage
{
    public void Remove(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        _images.TryRemove(id, out _);
    }

    public string StoreImage(byte[] data, string contentType)
    {
        var id = Guid.NewGuid().ToString("N");
        _images[id] = new StoredImage(data, contentType);
        return id;
    }

    public bool TryGetImage(string id, out StoredImage image) => _images.TryGetValue(id, out image);

    public sealed class StoredImage
    {
        public StoredImage(byte[] data, string contentType)
        {
            Data = data;
            ContentType = contentType;
        }

        public string ContentType
        {
            get;
        }

        public byte[] Data
        {
            get;
        }

        public string ToDataUrl() => $"data:{ContentType};base64,{Convert.ToBase64String(Data)}";
    }

    private readonly ConcurrentDictionary<string, StoredImage> _images = new();
}

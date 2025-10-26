using System.Collections.Concurrent;

namespace Toolbox.Helpers;

/// <summary>
///     Verwaltet aufgenommene oder hochgeladene Bilder temporär im Speicher, sodass Komponenten
///     lediglich eine eindeutige Kennung austauschen müssen. Die Ablage erfolgt threadsicher in einer
///     ConcurrentDictionary-Instanz.
/// </summary>
public class TemporaryImageStorage
{
    /// <summary>
    ///     Entfernt ein zuvor gespeichertes Bild. Ungültige oder leere Kennungen werden still ignoriert,
    ///     damit Aufrufer keinen zusätzlichen Prüfaufwand haben.
    /// </summary>
    public void Remove(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        _images.TryRemove(id, out _);
    }

    /// <summary>
    ///     Speichert ein Bild unter einer neu generierten Kennung und liefert diese zur weiteren
    ///     Verwendung zurück.
    /// </summary>
    public string StoreImage(byte[] data, string contentType)
    {
        var id = Guid.NewGuid().ToString("N");
        _images[id] = new StoredImage(data, contentType);
        return id;
    }

    /// <summary>
    ///     Ruft ein Bild anhand seiner Kennung ab, ohne es aus dem Speicher zu entfernen.
    /// </summary>
    public bool TryGetImage(string id, out StoredImage image) => _images.TryGetValue(id, out image);

    /// <summary>
    ///     Kapselt die Binärdaten eines gespeicherten Bildes samt MIME-Typ und bietet Helfer zum
    ///     Erzeugen einer data:-URL.
    /// </summary>
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

        /// <summary>
        ///     Baut aus den gespeicherten Daten eine data:-URL, die unmittelbar in <img>-Tags verwendet
        ///     werden kann.
        /// </summary>
        public string ToDataUrl() => $"data:{ContentType};base64,{Convert.ToBase64String(Data)}";
    }

    private readonly ConcurrentDictionary<string, StoredImage> _images = new();
}

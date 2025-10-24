using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Helpers;

/// <summary>
/// Copies the built-in card deck from embedded resources into IndexedDB on startup.
/// </summary>
public sealed class DeckBootstrapper
{
    private const string DeckName = "TarotDeck_Wikipedia";
    private const int StreamBufferSize = 16 * 1024;

    private static readonly Assembly ResourceAssembly = typeof(DisplayTexts).Assembly;
    private static readonly string DeckResourcePrefix = $"{typeof(DisplayTexts).Namespace}.Images.{DeckName}.";

    private readonly IndexedDbHelper dbHelper;
    private readonly ILogger<DeckBootstrapper>? logger;

    public DeckBootstrapper(IndexedDbHelper dbHelper, ILogger<DeckBootstrapper>? logger = null)
    {
        this.dbHelper = dbHelper ?? throw new ArgumentNullException(nameof(dbHelper));
        this.logger = logger;
    }

    /// <summary>
    /// Ensures the built-in card deck exists in IndexedDB.
    /// </summary>
    public async Task EnsureDefaultDeckAsync()
    {
        await dbHelper.InitializeAsync().ConfigureAwait(false);

        var existingDeck = await dbHelper.GetDeckAsync(DeckName).ConfigureAwait(false);
        if (existingDeck is not null)
        {
            logger?.LogDebug("Deck '{Deck}' already exists in IndexedDB.", DeckName);
            return;
        }

        logger?.LogInformation("Importing deck '{Deck}' from embedded resources.", DeckName);

        var cards = await LoadCardsFromResourcesAsync().ConfigureAwait(false);

        await dbHelper.CreateDeckAsync(new Deck
        {
            Id = DeckName,
            Name = DeckName
        }).ConfigureAwait(false);

        foreach (var card in cards)
        {
            await dbHelper.CreateCardAsync(new Spielkarte
            {
                Id = card.Id,
                DeckId = DeckName,
                Image = card.Image,
                Description = card.Description
            }).ConfigureAwait(false);
        }

        logger?.LogInformation("Successfully imported deck '{Deck}' with {Count} cards.", DeckName, cards.Count);
    }

    private static async Task<List<CardResource>> LoadCardsFromResourcesAsync()
    {
        var resourceNames = ResourceAssembly.GetManifestResourceNames();

        var imageResources = resourceNames
            .Where(IsImageResource)
            .ToDictionary(GetCardIdFromResourceName, name => name, StringComparer.OrdinalIgnoreCase);

        var descriptionResources = resourceNames
            .Where(IsDescriptionResource)
            .ToDictionary(GetCardIdFromResourceName, name => name, StringComparer.OrdinalIgnoreCase);

        var cards = new List<CardResource>(descriptionResources.Count);

        foreach (var (cardId, descriptionResourceName) in descriptionResources.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (cardId.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            if (!imageResources.TryGetValue(cardId, out var imageResourceName))
            {
                throw new InvalidOperationException($"Image resource for card '{cardId}' was not found.");
            }

            var imageBytes = await ReadAllBytesAsync(imageResourceName).ConfigureAwait(false);
            var description = await ReadAllTextAsync(descriptionResourceName).ConfigureAwait(false);

            cards.Add(new CardResource(cardId, imageBytes, description));
        }

        return cards;
    }

    private static bool IsImageResource(string resourceName) =>
        resourceName.StartsWith(DeckResourcePrefix, StringComparison.OrdinalIgnoreCase)
        && (resourceName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || resourceName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

    private static bool IsDescriptionResource(string resourceName) =>
        resourceName.StartsWith(DeckResourcePrefix, StringComparison.OrdinalIgnoreCase)
        && resourceName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    private static string GetCardIdFromResourceName(string resourceName)
    {
        var relativeName = resourceName[DeckResourcePrefix.Length..];
        var fileName = Path.GetFileName(relativeName);
        var cardId = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw new InvalidOperationException($"Unable to determine card id from resource '{resourceName}'.");
        }

        return cardId;
    }

    private static async Task<byte[]> ReadAllBytesAsync(string resourceName)
    {
        await using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, StreamBufferSize).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    private static async Task<string> ReadAllTextAsync(string resourceName)
    {
        await using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private sealed record CardResource(string Id, byte[] Image, string Description);
}

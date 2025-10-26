using Markdig;
using System.Collections.Concurrent;
using System.Reflection;

namespace Toolbox.Helpers;

/// <summary>
///     Lädt Hilfetexte aus eingebetteten Markdown-Ressourcen und wandelt sie in HTML um.
/// </summary>
public sealed class HelpContentProvider
{
    public HelpContentProvider()
    {
        resourceLookup = new Lazy<Dictionary<string, string>>(BuildResourceLookup, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    ///     Liefert den Hilfetext für den angegebenen Schlüssel als HTML-Markup.
    /// </summary>
    /// <param name="key"> Der logische Schlüssel, in der Regel die ID des Steuerelements. </param>
    public Task<string?> GetHelpHtmlAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<string?>(null);
        }

        var normalizedKey = key.Trim();

        if (htmlCache.TryGetValue(normalizedKey, out var cachedHtml))
        {
            return Task.FromResult<string?>(cachedHtml);
        }

        if (!resourceLookup.Value.TryGetValue(normalizedKey, out var resourceName))
        {
            return Task.FromResult<string?>(null);
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            return Task.FromResult<string?>(null);
        }

        using var reader = new StreamReader(stream);
        var markdown = reader.ReadToEnd();
        var html = Markdown.ToHtml(markdown, Pipeline);
        htmlCache[normalizedKey] = html;
        return Task.FromResult<string?>(html);
    }

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private readonly Assembly assembly = typeof(HelpContentProvider).Assembly;
    private readonly ConcurrentDictionary<string, string> htmlCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<Dictionary<string, string>> resourceLookup;

    private static string? ExtractKeyFromResourceName(string resourceName)
    {
        var helpIndex = resourceName.IndexOf(".Help.", StringComparison.OrdinalIgnoreCase);

        if (helpIndex < 0)
        {
            return null;
        }

        var keyStart = helpIndex + ".Help.".Length;
        var key = resourceName[keyStart..^3];
        return key;
    }

    private Dictionary<string, string> BuildResourceLookup()
    {
        var resources = assembly.GetManifestResourceNames();
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in resources)
        {
            if (!resource.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = ExtractKeyFromResourceName(resource);

            if (!string.IsNullOrEmpty(key))
            {
                lookup[key] = resource;
            }
        }

        return lookup;
    }
}

using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Toolbox;

internal static class BuildInfo
{
    private const string BuildNumberKey = "BuildNumber";
    private const string BuildTimestampKey = "BuildTimestamp";

    private static readonly Assembly Assembly = typeof(BuildInfo).Assembly;

    public static string Version => Assembly.GetName().Version?.ToString() ?? "unbekannt";

    public static string BuildNumber => GetAssemblyMetadata(BuildNumberKey) ?? Version;

    public static string BuildTimestamp => FormatTimestamp(GetAssemblyMetadata(BuildTimestampKey));

    private static string? GetAssemblyMetadata(string key) => Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == key)?.Value;

    private static string FormatTimestamp(string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return "unbekannt";
        }

        if (DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedTimestamp))
        {
            return parsedTimestamp.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture);
        }

        return timestamp;
    }
}

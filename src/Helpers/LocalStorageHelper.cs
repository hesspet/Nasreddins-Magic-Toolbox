using Microsoft.JSInterop;
using System.Text.Json;

namespace Toolbox.Helpers;

/// <summary>
///     Provides strongly typed helpers for accessing the browser local storage.
/// </summary>
public class LocalStorageHelper
{
    public LocalStorageHelper(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    ///     Reads a value from the browser local storage.
    /// </summary>
    public async Task<T?> GetItemAsync<T>(string key)
    {
        var result = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);

        if (string.IsNullOrEmpty(result))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(result, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    /// <summary>
    ///     Removes a value from the browser local storage.
    /// </summary>
    public async Task RemoveItemAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }

    /// <summary>
    ///     Stores a value in the browser local storage.
    /// </summary>
    public async Task SetItemAsync<T>(string key, T value)
    {
        var payload = JsonSerializer.Serialize(value, SerializerOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, payload);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _jsRuntime;
}

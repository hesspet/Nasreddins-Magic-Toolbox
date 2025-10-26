using Microsoft.JSInterop;
using Toolbox.Helpers;

namespace Toolbox.Settings;

/// <summary>
///     Provides centralized theme management and persists the selected preference.
/// </summary>
public class ThemeService
{
    public ThemeService(IJSRuntime jsRuntime, LocalStorageHelper localStorage)
    {
        _jsRuntime = jsRuntime;
        _localStorage = localStorage;
    }

    public event Action<ThemePreference>? ThemeChanged;

    public ThemePreference CurrentTheme => _currentTheme;

    public Task EnsureInitializedAsync()
    {
        _initializationTask ??= InitializeAsyncCore();
        return _initializationTask;
    }

    public async Task SetThemeAsync(ThemePreference theme)
    {
        await EnsureInitializedAsync();

        if (_currentTheme == theme)
        {
            return;
        }

        _currentTheme = theme;
        await _localStorage.SetItemAsync(ApplicationSettings.ThemePreferenceKey, theme.ToString());
        await ApplyThemeAsync(theme);
        ThemeChanged?.Invoke(theme);
    }

    private readonly IJSRuntime _jsRuntime;
    private readonly LocalStorageHelper _localStorage;
    private ThemePreference _currentTheme = ApplicationSettings.ThemePreferenceDefault;
    private Task? _initializationTask;

    private async Task ApplyThemeAsync(ThemePreference theme)
    {
        var themeName = theme == ThemePreference.Dark ? "dark" : "light";
        await _jsRuntime.InvokeVoidAsync("theme.applyTheme", themeName);
    }

    private async Task InitializeAsyncCore()
    {
        var storedValue = await _localStorage.GetItemAsync<string>(ApplicationSettings.ThemePreferenceKey);

        if (!string.IsNullOrWhiteSpace(storedValue) &&
            Enum.TryParse(storedValue, true, out ThemePreference parsedTheme))
        {
            _currentTheme = parsedTheme;
        }
        else
        {
            await _localStorage.SetItemAsync(ApplicationSettings.ThemePreferenceKey, _currentTheme.ToString());
        }

        await ApplyThemeAsync(_currentTheme);
    }
}

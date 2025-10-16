using Microsoft.AspNetCore.Components;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages;

public partial class SettingsCommon : SettingsPageBase, IDisposable
{
    protected override void OnInitialized()
    {
        UpdatePageTitle(DisplayTexts.SettingsPageTitle);
    }

    protected override async Task OnInitializedAsync()
    {
        await ThemeService.EnsureInitializedAsync();
        selectedTheme = ThemeService.CurrentTheme;
        ThemeService.ThemeChanged += HandleThemeChanged;

        var storedDuration = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.SplashScreenDurationKey);

        if (storedDuration.HasValue && Array.IndexOf(ApplicationSettings.SplashScreenDurationOptions, storedDuration.Value) >= 0)
        {
            selectedDuration = storedDuration.Value;
        }
        else
        {
            await LocalStorage.SetItemAsync(ApplicationSettings.SplashScreenDurationKey, selectedDuration);
        }

        var storedUpdatePreference = await LocalStorage.GetItemAsync<bool?>(ApplicationSettings.CheckForUpdatesOnStartupKey);

        if (storedUpdatePreference.HasValue)
        {
            checkForUpdatesOnStartup = storedUpdatePreference.Value;
        }
        else
        {
            await LocalStorage.SetItemAsync(ApplicationSettings.CheckForUpdatesOnStartupKey, checkForUpdatesOnStartup);
        }
    }

    private bool checkForUpdatesOnStartup = ApplicationSettings.CheckForUpdatesOnStartupDefault;

    private ThemePreference selectedTheme = ApplicationSettings.ThemePreferenceDefault;

    private int selectedDuration = ApplicationSettings.SplashScreenDurationDefaultSeconds;

    [Inject]
    private ThemeService ThemeService { get; set; } = default!;

    private void HandleThemeChanged(ThemePreference theme)
    {
        selectedTheme = theme;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task OnCheckForUpdatesChanged(ChangeEventArgs args)
    {
        if (args.Value is bool boolValue)
        {
            checkForUpdatesOnStartup = boolValue;
        }
        else if (args.Value is string stringValue && bool.TryParse(stringValue, out var parsedValue))
        {
            checkForUpdatesOnStartup = parsedValue;
        }
        else
        {
            return;
        }

        await LocalStorage.SetItemAsync(ApplicationSettings.CheckForUpdatesOnStartupKey, checkForUpdatesOnStartup);
        StateHasChanged();
    }

    private async Task OnDurationChanged(ChangeEventArgs args)
    {
        if (args.Value is null)
        {
            return;
        }

        if (int.TryParse(args.Value.ToString(), out var duration) && Array.IndexOf(ApplicationSettings.SplashScreenDurationOptions, duration) >= 0)
        {
            selectedDuration = duration;
            await LocalStorage.SetItemAsync(ApplicationSettings.SplashScreenDurationKey, selectedDuration);
            StateHasChanged();
        }
    }

    private async Task OnThemePreferenceChanged(ChangeEventArgs args)
    {
        if (args.Value is null)
        {
            return;
        }

        if (!Enum.TryParse(args.Value.ToString(), true, out ThemePreference theme))
        {
            return;
        }

        if (selectedTheme == theme)
        {
            return;
        }

        selectedTheme = theme;
        await ThemeService.SetThemeAsync(theme);
        StateHasChanged();
    }

    public void Dispose()
    {
        ThemeService.ThemeChanged -= HandleThemeChanged;
    }
}

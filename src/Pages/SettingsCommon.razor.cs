using System;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Models;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages;

public partial class SettingsCommon : SettingsPageBase, IDisposable
{
    protected override void OnInitialized()
    {
        LogService.LogDebug($"Seite '{DisplayTexts.SettingsPageTitle}' initialisiert.");
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

        var storedLogMaxLines = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.LogMaxLinesKey);

        if (storedLogMaxLines.HasValue)
        {
            logMaxLines = ApplicationSettings.ClampLogMaxLines(storedLogMaxLines.Value);

            if (storedLogMaxLines.Value != logMaxLines)
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.LogMaxLinesKey, logMaxLines);
            }
        }
        else
        {
            await LocalStorage.SetItemAsync(ApplicationSettings.LogMaxLinesKey, logMaxLines);
        }

        var storedLogLevel = await LocalStorage.GetItemAsync<string?>(ApplicationSettings.LogLevelKey);

        if (!string.IsNullOrWhiteSpace(storedLogLevel) && ApplicationSettings.TryParseLogLevel(storedLogLevel, out var parsedLevel))
        {
            selectedLogLevel = parsedLevel;
            LogService.SetLogLevel(selectedLogLevel);

            if (!string.Equals(storedLogLevel, selectedLogLevel.ToString(), StringComparison.Ordinal))
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.LogLevelKey, selectedLogLevel.ToString());
            }
        }
        else
        {
            selectedLogLevel = ApplicationSettings.LogLevelDefault;
            LogService.SetLogLevel(selectedLogLevel);
            await LocalStorage.SetItemAsync(ApplicationSettings.LogLevelKey, selectedLogLevel.ToString());
        }

        LogService.SetMaxEntries(logMaxLines);
    }

    private bool checkForUpdatesOnStartup = ApplicationSettings.CheckForUpdatesOnStartupDefault;

    private ThemePreference selectedTheme = ApplicationSettings.ThemePreferenceDefault;

    private int selectedDuration = ApplicationSettings.SplashScreenDurationDefaultSeconds;

    private int logMaxLines = ApplicationSettings.LogMaxLinesDefault;
    private LogLevel selectedLogLevel = ApplicationSettings.LogLevelDefault;

    [Inject]
    private ThemeService ThemeService { get; set; } = default!;

    [Inject]
    private InMemoryLogService LogService { get; set; } = default!;

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

    private async Task OnLogMaxLinesChanged(ChangeEventArgs args)
    {
        if (args.Value is null)
        {
            return;
        }

        if (!int.TryParse(args.Value.ToString(), out var parsedValue))
        {
            return;
        }

        var clampedValue = ApplicationSettings.ClampLogMaxLines(parsedValue);

        if (logMaxLines == clampedValue && parsedValue == clampedValue)
        {
            return;
        }

        logMaxLines = clampedValue;
        await LocalStorage.SetItemAsync(ApplicationSettings.LogMaxLinesKey, logMaxLines);
        LogService.SetMaxEntries(logMaxLines);
        StateHasChanged();
    }

    private async Task OnLogLevelChanged(ChangeEventArgs args)
    {
        if (args.Value is null)
        {
            return;
        }

        if (!ApplicationSettings.TryParseLogLevel(args.Value.ToString(), out var parsedLevel))
        {
            return;
        }

        if (selectedLogLevel == parsedLevel)
        {
            return;
        }

        selectedLogLevel = parsedLevel;
        LogService.SetLogLevel(selectedLogLevel);
        await LocalStorage.SetItemAsync(ApplicationSettings.LogLevelKey, selectedLogLevel.ToString());
        StateHasChanged();
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

    private static string GetLogLevelOptionLabel(LogLevel level) => level switch
    {
        LogLevel.None => DisplayTexts.SettingsLoggingLogLevelOptionNone,
        LogLevel.Error => DisplayTexts.SettingsLoggingLogLevelOptionError,
        LogLevel.Warn => DisplayTexts.SettingsLoggingLogLevelOptionWarn,
        LogLevel.Info => DisplayTexts.SettingsLoggingLogLevelOptionInfo,
        LogLevel.Debug => DisplayTexts.SettingsLoggingLogLevelOptionDebug,
        _ => level.ToString(),
    };
}

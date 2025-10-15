using Microsoft.AspNetCore.Components;
using Toolbox.Components;
using Toolbox.Helpers;
using Toolbox.Layout;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages
{
    public partial class Settings : IDisposable
    {
        protected override void OnInitialized()
        {
            Layout?.UpdateCurrentPageTitle(DisplayTexts.SettingsPageTitle);
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

            var storedCardScale = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.CardScalePercentKey);

            if (storedCardScale.HasValue)
            {
                cardScalePercent = ApplicationSettings.ClampCardScalePercent(storedCardScale.Value);

                if (cardScalePercent != storedCardScale.Value)
                {
                    await LocalStorage.SetItemAsync(ApplicationSettings.CardScalePercentKey, cardScalePercent);
                }
            }
            else
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.CardScalePercentKey, cardScalePercent);
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

            var storedSearchAutoClear = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.SearchAutoClearDelaySecondsKey);

            if (storedSearchAutoClear.HasValue)
            {
                searchAutoClearDelaySeconds = ApplicationSettings.ClampSearchAutoClearDelaySeconds(storedSearchAutoClear.Value);

                if (searchAutoClearDelaySeconds != storedSearchAutoClear.Value)
                {
                    await LocalStorage.SetItemAsync(ApplicationSettings.SearchAutoClearDelaySecondsKey, searchAutoClearDelaySeconds);
                }
            }
            else
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.SearchAutoClearDelaySecondsKey, searchAutoClearDelaySeconds);
            }
        }

        private int cardScalePercent = ApplicationSettings.CardScalePercentDefault;

        private bool checkForUpdatesOnStartup = ApplicationSettings.CheckForUpdatesOnStartupDefault;

        private ThemePreference selectedTheme = ApplicationSettings.ThemePreferenceDefault;

        private int selectedDuration = ApplicationSettings.SplashScreenDurationDefaultSeconds;

        private int searchAutoClearDelaySeconds = ApplicationSettings.SearchAutoClearDelayDefaultSeconds;

        [Inject]
        private ThemeService ThemeService { get; set; } = default!;

        private HelpDialog? helpDialog;

        [CascadingParameter]
        private MainLayout? Layout
        {
            get; set;
        }

        private string GetHelpButtonLabel(string controlLabel) => HelpDialog.GetButtonLabel(controlLabel);

        private void HandleThemeChanged(ThemePreference theme)
        {
            selectedTheme = theme;
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task OnCardScaleChanged(ChangeEventArgs args)
        {
            if (args.Value is null)
            {
                return;
            }

            if (int.TryParse(args.Value.ToString(), out var newValue))
            {
                await UpdateCardScaleAsync(newValue);
            }
        }

        private async Task OnCardScaleNumberChanged(ChangeEventArgs args)
        {
            if (args.Value is null)
            {
                return;
            }

            if (int.TryParse(args.Value.ToString(), out var newValue))
            {
                await UpdateCardScaleAsync(newValue);
            }
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

        private async Task OnSearchAutoClearDelayChanged(ChangeEventArgs args)
        {
            if (args.Value is null)
            {
                return;
            }

            if (!int.TryParse(args.Value.ToString(), out var delay))
            {
                return;
            }

            var clamped = ApplicationSettings.ClampSearchAutoClearDelaySeconds(delay);

            if (searchAutoClearDelaySeconds == clamped)
            {
                return;
            }

            searchAutoClearDelaySeconds = clamped;
            await LocalStorage.SetItemAsync(ApplicationSettings.SearchAutoClearDelaySecondsKey, searchAutoClearDelaySeconds);
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

        private Task ShowHelpAsync(string helpKey, string helpTitle) => helpDialog?.ShowAsync(helpKey, helpTitle) ?? Task.CompletedTask;

        private async Task UpdateCardScaleAsync(int newValue)
        {
            var clamped = ApplicationSettings.ClampCardScalePercent(newValue);

            if (cardScalePercent == clamped)
            {
                return;
            }

            cardScalePercent = clamped;
            await LocalStorage.SetItemAsync(ApplicationSettings.CardScalePercentKey, cardScalePercent);
            StateHasChanged();
        }

        public void Dispose()
        {
            ThemeService.ThemeChanged -= HandleThemeChanged;
        }
    }
}

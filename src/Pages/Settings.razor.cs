using Microsoft.AspNetCore.Components;
using Toolbox.Layout;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages
{
    public partial class Settings
    {
        [CascadingParameter]
        private MainLayout? Layout { get; set; }

        protected override void OnInitialized()
        {
            Layout?.UpdateCurrentPageTitle(DisplayTexts.SettingsPageTitle);
        }

        protected override async Task OnInitializedAsync()
        {
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
        private int selectedDuration = ApplicationSettings.SplashScreenDurationDefaultSeconds;

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
    }
}

using System.Net;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Layout;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages
{
    public partial class Settings
    {
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

        private string currentHelpTitle = string.Empty;

        private MarkupString helpContent = new(string.Empty);

        private bool isHelpVisible;

        private int selectedDuration = ApplicationSettings.SplashScreenDurationDefaultSeconds;

        private int searchAutoClearDelaySeconds = ApplicationSettings.SearchAutoClearDelayDefaultSeconds;

        [Inject]
        private HelpContentProvider HelpContentProviderInst { get; set; } = default!;

        [CascadingParameter]
        private MainLayout? Layout
        {
            get; set;
        }

        private void CloseHelp()
        {
            isHelpVisible = false;
            StateHasChanged();
        }

        private string GetHelpButtonLabel(string controlLabel) => string.Format(DisplayTexts.SettingsHelpButtonLabelFormat, controlLabel);

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

        private async Task ShowHelpAsync(string helpKey, string helpTitle)
        {
            currentHelpTitle = helpTitle;
            var html = await HelpContentProviderInst.GetHelpHtmlAsync(helpKey);

            if (string.IsNullOrWhiteSpace(html))
            {
                var fallback = WebUtility.HtmlEncode(DisplayTexts.SettingsHelpNotFoundMessage);
                helpContent = new MarkupString($"<p>{fallback}</p>");
            }
            else
            {
                helpContent = new MarkupString(html);
            }

            isHelpVisible = true;
            StateHasChanged();
        }

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
    }
}

using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages;

public partial class SettingsDecks : SettingsPageBase
{
    protected override void OnInitialized()
    {
        LogService.LogDebug($"Seite '{DisplayTexts.SettingsDecksPageTitle}' initialisiert.");
        UpdatePageTitle(DisplayTexts.SettingsDecksPageTitle);
    }

    protected override async Task OnInitializedAsync()
    {
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

    private int searchAutoClearDelaySeconds = ApplicationSettings.SearchAutoClearDelayDefaultSeconds;

    [Inject]
    private InMemoryLogService LogService { get; set; } = default!;

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

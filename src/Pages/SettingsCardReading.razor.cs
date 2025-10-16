using Microsoft.AspNetCore.Components;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages;

public partial class SettingsCardReading : SettingsPageBase
{
    private string gptApiKey = string.Empty;
    private string chatGptApiUrl = string.Empty;

    protected override void OnInitialized()
    {
        UpdatePageTitle(DisplayTexts.SettingsCardReadingPageTitle);
    }

    protected override async Task OnInitializedAsync()
    {
        var storedApiKey = await LocalStorage.GetItemAsync<string?>(ApplicationSettings.CardReadingGptApiKeyKey);
        if (storedApiKey is not null)
        {
            gptApiKey = storedApiKey;
        }
        else
        {
            gptApiKey = ApplicationSettings.CardReadingGptApiKeyDefault;
            await LocalStorage.SetItemAsync(ApplicationSettings.CardReadingGptApiKeyKey, gptApiKey);
        }

        var storedApiUrl = await LocalStorage.GetItemAsync<string?>(ApplicationSettings.CardReadingChatGptApiUrlKey);
        if (storedApiUrl is not null)
        {
            chatGptApiUrl = storedApiUrl;
        }
        else
        {
            chatGptApiUrl = ApplicationSettings.CardReadingChatGptApiUrlDefault;
            await LocalStorage.SetItemAsync(ApplicationSettings.CardReadingChatGptApiUrlKey, chatGptApiUrl);
        }
    }

    private async Task OnApiKeyChanged(ChangeEventArgs args)
    {
        gptApiKey = args.Value?.ToString() ?? string.Empty;
        await LocalStorage.SetItemAsync(ApplicationSettings.CardReadingGptApiKeyKey, gptApiKey);
        StateHasChanged();
    }

    private async Task OnApiUrlChanged(ChangeEventArgs args)
    {
        chatGptApiUrl = args.Value?.ToString() ?? string.Empty;
        await LocalStorage.SetItemAsync(ApplicationSettings.CardReadingChatGptApiUrlKey, chatGptApiUrl);
        StateHasChanged();
    }
}

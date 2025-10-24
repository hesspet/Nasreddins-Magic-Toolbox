using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Resources;
using Toolbox.Settings;

namespace Toolbox.Pages;

public partial class SettingsCardReading : SettingsPageBase
{
    protected override void OnInitialized()
    {
        LogService.LogDebug($"Seite '{DisplayTexts.SettingsCardReadingPageTitle}' initialisiert.");
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

    private static readonly JsonSerializerOptions ChatSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private string chatGptApiUrl = string.Empty;
    private string gptApiKey = string.Empty;
    private bool isSendingTest;
    private string? testErrorMessage;
    private string testResponse = string.Empty;

    [Inject]
    private HttpClient HttpClient { get; set; } = default!;

    [Inject]
    private InMemoryLogService LogService { get; set; } = default!;

    private static bool TryBuildRequestUri(string url, out Uri? requestUri)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out requestUri))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate($"https://{url}", UriKind.Absolute, out requestUri))
        {
            return true;
        }

        requestUri = null;
        return false;
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

    private async Task SendChatGptTestAsync()
    {
        if (isSendingTest)
        {
            return;
        }

        if (!TryBuildRequestUri(chatGptApiUrl, out var requestUri))
        {
            testErrorMessage = "Die angegebene ChatGPT-URL ist ungültig.";
            testResponse = string.Empty;
            StateHasChanged();
            return;
        }

        isSendingTest = true;
        testErrorMessage = null;
        testResponse = string.Empty;
        StateHasChanged();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(new ChatRequest
                {
                    Model = "gpt-4o-mini",
                    Messages = new List<ChatMessage>
                    {
                        new() { Role = "user", Content = "Hallo" },
                    },
                }, options: ChatSerializerOptions),
            };

            if (!string.IsNullOrWhiteSpace(gptApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gptApiKey);
            }

            using var response = await HttpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var chatResponse = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(ChatSerializerOptions);
                if (chatResponse?.Choices?.Count > 0)
                {
                    testResponse = chatResponse.Choices[0].Message?.Content?.Trim() ?? string.Empty;
                }
                else
                {
                    testResponse = (await response.Content.ReadAsStringAsync()).Trim();
                    LogService.LogInformation(nameof(testResponse) + "=" + testResponse);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                testErrorMessage = $"Fehler {response.StatusCode:D} ({response.StatusCode}): {errorContent}";

                LogService.LogError(nameof(testErrorMessage) + "=" + testErrorMessage);
            }
        }
        catch (Exception ex)
        {
            testErrorMessage = $"Fehler beim Senden der Testnachricht: {ex.Message}";
            LogService.LogError(nameof(testErrorMessage) + "=" + testErrorMessage + ex.StackTrace);
        }
        finally
        {
            isSendingTest = false;
            StateHasChanged();
        }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message
        {
            get; set;
        }
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice> Choices { get; set; } = new();
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("content")]
        public string? Content
        {
            get; set;
        }

        [JsonPropertyName("role")]
        public string? Role
        {
            get; set;
        }
    }

    private sealed class ChatRequest
    {
        [JsonPropertyName("messages")]
        public IList<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        [JsonPropertyName("model")]
        public string? Model
        {
            get; set;
        }
    }
}

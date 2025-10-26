using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Toolbox.Helpers;
using Toolbox.Layout;
using Toolbox.Resources;
using Toolbox.Services;
using Toolbox.Settings;

namespace Toolbox.Pages;

public partial class Logging : ComponentBase, IDisposable
{
    private string logContent = string.Empty;
    private bool isLogEmpty = true;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    [Inject]
    private InMemoryLogService LogService { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private LocalStorageHelper LocalStorage { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        LogService.LogDebug($"Seite '{DisplayTexts.LoggingPageTitle}' initialisiert.");
        Layout?.UpdateCurrentPageTitle(DisplayTexts.LoggingPageTitle);
        LogService.LogsChanged += HandleLogsChanged;

        await EnsureLogLimitInitializedAsync();
        UpdateLogContent();
    }

    private async Task EnsureLogLimitInitializedAsync()
    {
        var storedMaxLines = await LocalStorage.GetItemAsync<int?>(ApplicationSettings.LogMaxLinesKey);

        if (storedMaxLines.HasValue)
        {
            var clamped = ApplicationSettings.ClampLogMaxLines(storedMaxLines.Value);
            LogService.SetMaxEntries(clamped);

            if (clamped != storedMaxLines.Value)
            {
                await LocalStorage.SetItemAsync(ApplicationSettings.LogMaxLinesKey, clamped);
            }
        }
        else
        {
            await LocalStorage.SetItemAsync(ApplicationSettings.LogMaxLinesKey, LogService.MaxEntries);
        }
    }

    private void HandleLogsChanged(object? sender, EventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            UpdateLogContent();
            StateHasChanged();
        });
    }

    private void UpdateLogContent()
    {
        logContent = LogService.GetLogText();
        isLogEmpty = string.IsNullOrEmpty(logContent);
    }

    private async Task CopyToClipboardAsync()
    {
        if (isLogEmpty)
        {
            return;
        }

        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", logContent);
    }

    private void ClearLog()
    {
        if (isLogEmpty)
        {
            return;
        }

        LogService.Clear();
    }

    public void Dispose()
    {
        LogService.LogsChanged -= HandleLogsChanged;
    }
}

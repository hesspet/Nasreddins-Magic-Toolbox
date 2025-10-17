using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Models;

namespace Toolbox.Components;

public partial class CardDeckSelector : ComponentBase
{
    private readonly string selectId = $"deckSelector-{Guid.NewGuid():N}";
    private CardDeckDataProvider? dataProvider;
    private IReadOnlyList<DeckOption> deckOptions = Array.Empty<DeckOption>();
    private bool isLoading = true;
    private string selectedDeckId = string.Empty;

    [Inject]
    private IndexedDbHelper DbHelper { get; set; } = default!;

    [Inject]
    private LocalStorageHelper LocalStorage { get; set; } = default!;

    [Parameter]
    public string StorageKey { get; set; } = string.Empty;

    [Parameter]
    public string CssClass { get; set; } = "card-deck-selector";

    [Parameter]
    public EventCallback<DeckOption?> DeckSelectionChanged { get; set; }

    protected override async Task OnInitializedAsync()
    {
        dataProvider = new CardDeckDataProvider(DbHelper);
        await LoadOptionsAsync();
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            isLoading = true;
            deckOptions = await dataProvider!.LoadDeckOptionsAsync();
            await RestoreSelectionFromStorageAsync();
        }
        finally
        {
            isLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task RestoreSelectionFromStorageAsync()
    {
        var storedValue = string.Empty;
        if (!string.IsNullOrWhiteSpace(StorageKey))
        {
            storedValue = await LocalStorage.GetItemAsync<string?>(StorageKey) ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(storedValue) && deckOptions.Any(option => string.Equals(option.DeckId, storedValue, StringComparison.OrdinalIgnoreCase)))
        {
            selectedDeckId = storedValue;
        }
        else
        {
            selectedDeckId = string.Empty;
            if (!string.IsNullOrWhiteSpace(StorageKey))
            {
                await LocalStorage.SetItemAsync(StorageKey, string.Empty);
            }
        }

        await NotifySelectionChangedAsync();
    }

    private async Task HandleSelectionChangedAsync(ChangeEventArgs args)
    {
        var newValue = args.Value?.ToString() ?? string.Empty;
        if (string.Equals(newValue, selectedDeckId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        selectedDeckId = newValue;

        if (!string.IsNullOrWhiteSpace(StorageKey))
        {
            await LocalStorage.SetItemAsync(StorageKey, selectedDeckId);
        }

        await NotifySelectionChangedAsync();
    }

    private async Task NotifySelectionChangedAsync()
    {
        DeckOption? selectedOption = null;
        if (!string.IsNullOrWhiteSpace(selectedDeckId))
        {
            selectedOption = deckOptions.FirstOrDefault(option => string.Equals(option.DeckId, selectedDeckId, StringComparison.OrdinalIgnoreCase));
        }

        if (!DeckSelectionChanged.HasDelegate)
        {
            return;
        }

        await DeckSelectionChanged.InvokeAsync(selectedOption);
    }
}

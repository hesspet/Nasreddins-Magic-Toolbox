using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Components
{
    public partial class DeckSelectionBar : ComponentBase
    {
        private readonly string selectionElementId = $"deckSelection_{Guid.NewGuid():N}";
        private string? storageKey;
        private bool hasRestoredSelection;

        private string SelectionElementId => selectionElementId;

        private string CurrentSelection => SelectedDeck ?? string.Empty;

        private string SelectionLabelText => SelectionLabel ?? DisplayTexts.TarotDeckSelectionLabel;

        private string SelectionPlaceholderText => SelectionPlaceholder ?? DisplayTexts.TarotDeckSelectionPlaceholder;

        private string SearchButtonTextValue => string.IsNullOrWhiteSpace(SearchButtonText)
            ? DisplayTexts.TarotSearchLabel
            : SearchButtonText!;

        private string LoadingText => LoadingMessage ?? DisplayTexts.TarotDeckLoading;

        private bool IsSearchDisabled => !SearchEnabled || !ShowSearchButton || !OnSearchRequested.HasDelegate;

        [Parameter]
        public IEnumerable<DeckOption>? DeckOptions { get; set; }

        [Parameter]
        public string? SelectedDeck { get; set; }

        [Parameter]
        public EventCallback<string?> SelectedDeckChanged { get; set; }

        [Parameter]
        public bool IsLoading { get; set; }

        [Parameter]
        public bool ShowSearchButton { get; set; }

        [Parameter]
        public bool SearchEnabled { get; set; } = true;

        [Parameter]
        public string? SearchButtonText { get; set; }

        [Parameter]
        public string? SelectionLabel { get; set; }

        [Parameter]
        public string? SelectionPlaceholder { get; set; }

        [Parameter]
        public string? LoadingMessage { get; set; }

        [Parameter]
        public EventCallback OnSearchRequested { get; set; }

        [Parameter]
        public string? SelectionStorageKey { get; set; }

        [Inject]
        private LocalStorageHelper LocalStorage { get; set; } = default!;

        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;

        protected override async Task OnParametersSetAsync()
        {
            await base.OnParametersSetAsync();

            storageKey ??= CreateStorageKey();

            if (hasRestoredSelection || storageKey is null || IsLoading)
            {
                return;
            }

            if (DeckOptions is null || !DeckOptions.Any())
            {
                return;
            }

            hasRestoredSelection = true;
            var storedValue = await LocalStorage.GetItemAsync<string?>(storageKey);

            if (string.IsNullOrWhiteSpace(storedValue))
            {
                return;
            }

            if (DeckOptions.Any(option => string.Equals(option.DeckId, storedValue, StringComparison.Ordinal)))
            {
                if (!string.Equals(SelectedDeck, storedValue, StringComparison.Ordinal))
                {
                    await SelectedDeckChanged.InvokeAsync(storedValue);
                }
            }
            else
            {
                await LocalStorage.RemoveItemAsync(storageKey);
            }
        }

        private async Task HandleSelectionChanged(ChangeEventArgs args)
        {
            var newValue = args?.Value?.ToString() ?? string.Empty;
            if (string.Equals(newValue, SelectedDeck, StringComparison.Ordinal))
            {
                return;
            }

            await SelectedDeckChanged.InvokeAsync(newValue);
            await StoreSelectionAsync(newValue);
        }

        private async Task HandleSearchClicked()
        {
            if (!ShowSearchButton || !SearchEnabled || !OnSearchRequested.HasDelegate)
            {
                return;
            }

            await OnSearchRequested.InvokeAsync();
        }

        private string? CreateStorageKey()
        {
            if (!string.IsNullOrWhiteSpace(SelectionStorageKey))
            {
                return SelectionStorageKey;
            }

            if (NavigationManager is null)
            {
                return null;
            }

            var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                relativePath = "/";
            }

            var queryIndex = relativePath.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                relativePath = relativePath[..queryIndex];
            }

            var hashIndex = relativePath.IndexOf('#', StringComparison.Ordinal);
            if (hashIndex >= 0)
            {
                relativePath = relativePath[..hashIndex];
            }

            return $"DeckSelection:{relativePath}";
        }

        private async Task StoreSelectionAsync(string? deckId)
        {
            storageKey ??= CreateStorageKey();

            if (storageKey is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(deckId))
            {
                await LocalStorage.RemoveItemAsync(storageKey);
                return;
            }

            await LocalStorage.SetItemAsync(storageKey, deckId);
        }
    }
}


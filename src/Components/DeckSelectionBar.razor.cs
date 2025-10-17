using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Components
{
    public partial class DeckSelectionBar : ComponentBase
    {
        private readonly string selectionElementId = $"deckSelection_{Guid.NewGuid():N}";

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

        private async Task HandleSelectionChanged(ChangeEventArgs args)
        {
            var newValue = args?.Value?.ToString() ?? string.Empty;
            if (string.Equals(newValue, SelectedDeck, StringComparison.Ordinal))
            {
                return;
            }

            await SelectedDeckChanged.InvokeAsync(newValue);
        }

        private async Task HandleSearchClicked()
        {
            if (!ShowSearchButton || !SearchEnabled || !OnSearchRequested.HasDelegate)
            {
                return;
            }

            await OnSearchRequested.InvokeAsync();
        }
    }
}


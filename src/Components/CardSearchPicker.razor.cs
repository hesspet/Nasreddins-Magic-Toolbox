using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Components;

public partial class CardSearchPicker : ComponentBase
{
    private readonly string dialogTitleId = $"card-search-dialog-title-{Guid.NewGuid():N}";
    private readonly string searchInputId = $"card-search-input-{Guid.NewGuid():N}";
    private CardDeckDataProvider? dataProvider;
    private DeckCards? cachedDeckCards;
    private DeckCardInfo? currentCard;
    private string? lastDeckId;
    private bool isDialogVisible;
    private bool isSearching;
    private string searchTerm = string.Empty;
    private string? errorMessage;

    [Inject]
    private IndexedDbHelper DbHelper { get; set; } = default!;

    [Parameter]
    public string? DeckId { get; set; }

    [Parameter]
    public string DeckDisplayName { get; set; } = string.Empty;

    [Parameter]
    public string PlaceholderText { get; set; } = DisplayTexts.CardSearchPickerPlaceholder;

    [Parameter]
    public string DialogTitle { get; set; } = DisplayTexts.CardSearchDialogTitle;

    [Parameter]
    public string SearchButtonLabel { get; set; } = DisplayTexts.CardSearchDialogSearchButton;

    [Parameter]
    public string CancelButtonLabel { get; set; } = DisplayTexts.CardSearchDialogCancelButton;

    [Parameter]
    public string DeckMissingMessage { get; set; } = DisplayTexts.CardSearchDialogDeckMissing;

    [Parameter]
    public string NoCardsMessage { get; set; } = DisplayTexts.CardSearchDialogNoCardsInDeck;

    [Parameter]
    public EventCallback<DeckCardInfo?> SelectedCardChanged { get; set; }

    [Parameter]
    public DeckCardInfo? SelectedCard { get; set; }

    [Parameter]
    public EventCallback<DeckCardInfo?> CardSelected { get; set; }

    private bool IsDeckSelected => !string.IsNullOrWhiteSpace(DeckId);

    private string TriggerTitle => IsDeckSelected
        ? DisplayTexts.TarotSearchLabel
        : DeckMissingMessage;

    private string? CurrentDeckMessage => string.IsNullOrWhiteSpace(DeckDisplayName)
        ? null
        : string.Format(CultureInfo.CurrentCulture, DisplayTexts.CardSearchDialogCurrentDeckFormat, DeckDisplayName);

    protected override void OnInitialized()
    {
        dataProvider = new CardDeckDataProvider(DbHelper);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (!Equals(SelectedCard, currentCard))
        {
            currentCard = SelectedCard;
        }

        if (!string.Equals(lastDeckId, DeckId, StringComparison.OrdinalIgnoreCase))
        {
            lastDeckId = DeckId ?? string.Empty;
            cachedDeckCards = null;

            if (currentCard is not null)
            {
                currentCard = null;
                _ = InvokeAsync(() => NotifyCardChangedAsync(null));
            }
        }
    }

    private void OpenDialog()
    {
        errorMessage = null;
        searchTerm = string.Empty;
        isDialogVisible = true;
    }

    private Task CloseDialogAsync()
    {
        isDialogVisible = false;
        return InvokeAsync(StateHasChanged);
    }

    private Task CloseDialogFromOverlayAsync() => CloseDialogAsync();

    private async Task SubmitSearchAsync()
    {
        if (isSearching)
        {
            return;
        }

        if (!IsDeckSelected)
        {
            errorMessage = DeckMissingMessage;
            await InvokeAsync(StateHasChanged);
            return;
        }

        isSearching = true;
        errorMessage = null;
        await InvokeAsync(StateHasChanged);

        try
        {
            var deckCards = await GetDeckCardsAsync();
            if (deckCards is null)
            {
                errorMessage = DeckMissingMessage;
                return;
            }

            if (deckCards.Cards.Count == 0)
            {
                errorMessage = NoCardsMessage;
                return;
            }

            var normalized = CardDeckUtilities.NormalizeForComparison(searchTerm);
            var index = normalized.Length == 0
                ? 0
                : CardDeckUtilities.FindMatchingCardIndex(deckCards.Cards, deckCards.DeckId, normalized);

            if (normalized.Length > 0 && index < 0)
            {
                errorMessage = DisplayTexts.TarotSearchNotFound;
                return;
            }

            if (index < 0)
            {
                index = 0;
            }

            var card = deckCards.Cards[index];
            var cardInfo = CardDeckUtilities.CreateCardInfo(deckCards.DeckId, deckCards.DeckDisplayName, card);

            await NotifyCardChangedAsync(cardInfo);
            await CloseDialogAsync();
        }
        finally
        {
            isSearching = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task<DeckCards?> GetDeckCardsAsync()
    {
        if (!IsDeckSelected || DeckId is null)
        {
            return null;
        }

        if (cachedDeckCards is { } cached && string.Equals(cached.DeckId, DeckId, StringComparison.OrdinalIgnoreCase))
        {
            return cached;
        }

        var loaded = await dataProvider!.LoadDeckCardsAsync(DeckId);
        cachedDeckCards = loaded;
        return loaded;
    }

    private async Task NotifyCardChangedAsync(DeckCardInfo? card)
    {
        currentCard = card;

        if (SelectedCardChanged.HasDelegate)
        {
            await SelectedCardChanged.InvokeAsync(card);
        }

        if (CardSelected.HasDelegate)
        {
            await CardSelected.InvokeAsync(card);
        }

        await InvokeAsync(StateHasChanged);
    }
}

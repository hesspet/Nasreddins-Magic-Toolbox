using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Toolbox.Helpers;
using Toolbox.Layout;
using Toolbox.Models;
using Toolbox.Models.CardReading;

namespace Toolbox.Pages.CardReading;

public abstract class CardReadingPageBase : ComponentBase
{
    private IReadOnlyList<DeckOption> deckOptions = Array.Empty<DeckOption>();
    private readonly Dictionary<string, string> deckDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private string selectedDeck = string.Empty;
    private bool isLoadingDecks = true;
    private bool isDeckDataLoading;

    protected IReadOnlyList<DeckOption> DeckOptions => deckOptions;

    protected bool IsLoadingDecks => isLoadingDecks;

    protected string SelectedDeck
    {
        get => selectedDeck;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(selectedDeck, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            selectedDeck = normalized;

            if (string.IsNullOrWhiteSpace(normalized))
            {
                isDeckDataLoading = false;
                LogService.LogInformation("Kartenspielauswahl wurde zurückgesetzt.");
            }
            else
            {
                isDeckDataLoading = true;
                var displayName = GetDeckDisplayName(normalized);
                LogService.LogInformation($"Kartenspiel '{displayName}' ({normalized}) wurde ausgewählt.");
            }

            OnDeckSelectionChanged();
        }
    }

    protected bool HasSelectedDeck => !string.IsNullOrWhiteSpace(SelectedDeck);

    protected bool IsSearchEnabled => HasSelectedDeck && !isDeckDataLoading;

    protected abstract string PageTitleText { get; }

    protected abstract string PageHeaderText { get; }

    protected abstract TarotSpreadLayout SpreadDefinition { get; }

    [Inject]
    protected IndexedDbHelper DbHelper { get; set; } = default!;

    [Inject]
    protected InMemoryLogService LogService { get; set; } = default!;

    [CascadingParameter]
    protected MainLayout? Layout { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        LogService.LogDebug($"Seite '{PageTitleText}' initialisiert.");
        Layout?.UpdateCurrentPageTitle(PageTitleText);
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadDecksAsync();
    }

    protected virtual void OnDeckSelectionChanged()
    {
        StateHasChanged();
    }

    protected Task HandleDeckLoadingChanged(bool isLoading)
    {
        isDeckDataLoading = isLoading;
        StateHasChanged();
        return Task.CompletedTask;
    }

    protected virtual Task HandleSearchRequested() => Task.CompletedTask;

    protected string GetDeckDisplayName(string deckId)
    {
        if (deckDisplayNames.TryGetValue(deckId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return CardSearchHelper.CreateDeckDisplayName(deckId);
    }

    private async Task LoadDecksAsync()
    {
        try
        {
            isLoadingDecks = true;
            StateHasChanged();

            LogService.LogDebug("Starte das Laden der verfügbaren Kartenspiele aus der Datenbank.");
            await DbHelper.InitializeAsync();
            var decks = await DbHelper.GetAllDecksAsync();

            var orderedOptions = decks.Where(deck => !string.IsNullOrWhiteSpace(deck?.Id))
                                      .Select(deck =>
                                      {
                                          var deckId = deck!.Id!;
                                          var name = string.IsNullOrWhiteSpace(deck.Name) ? deckId : deck.Name!;
                                          return new DeckOption(deckId, CardSearchHelper.CreateDeckDisplayName(name));
                                      })
                                      .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
                                      .ToList();

            deckOptions = orderedOptions;
            deckDisplayNames.Clear();

            if (orderedOptions.Count > 0)
            {
                foreach (var option in orderedOptions)
                {
                    deckDisplayNames[option.DeckId] = option.DisplayName;
                }

                LogService.LogDebug($"Folgende Kartenspiele wurden geladen: {string.Join(", ", orderedOptions.Select(option => option.DisplayName))}.");
            }
            else
            {
                LogService.LogDebug("Es wurden keine Kartenspiele gefunden.");
            }

            selectedDeck = string.Empty;
        }
        catch (Exception exception)
        {
            LogService.LogError($"Fehler beim Laden der Kartenspiele: {exception.Message}");
            throw;
        }
        finally
        {
            isLoadingDecks = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}

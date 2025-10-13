using System.Globalization;
using Microsoft.AspNetCore.Components;
using Toolbox.Layout;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Pages
{
    public partial class DatenInfo
    {
        [CascadingParameter]
        private MainLayout? Layout { get; set; }

        protected override void OnInitialized()
        {
            Layout?.UpdateCurrentPageTitle(DisplayTexts.DataInfoPageTitle);
        }

        protected override async Task OnInitializedAsync()
        {
            await DbHelper.InitializeAsync();
            await LoadDecksAsync();
        }

        private IReadOnlyList<Deck> decks = Array.Empty<Deck>();
        private List<string> deleteLogEntries = new();
        private bool isDeletingDeck;
        private bool isLoadingDecks;
        private bool isLoadingReport;
        private string reportDeckName = string.Empty;
        private IReadOnlyList<CardReportEntry> reportEntries = Array.Empty<CardReportEntry>();
        private string? selectedDeckId;
        private bool showDeleteLog;
        private bool showReport;
        private bool CanDeleteDeck => !string.IsNullOrWhiteSpace(selectedDeckId) && !isLoadingDecks && !isDeletingDeck;
        private bool CanShowReport => !string.IsNullOrWhiteSpace(selectedDeckId) && !isLoadingDecks && !isLoadingReport && !isDeletingDeck;

        private Task CloseReport()
        {
            showReport = false;
            reportEntries = Array.Empty<CardReportEntry>();
            reportDeckName = string.Empty;
            return Task.CompletedTask;
        }

        private async Task DeleteSelectedDeckAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedDeckId) || isDeletingDeck)
            {
                return;
            }

            var deckId = selectedDeckId;
            isDeletingDeck = true;
            showReport = false;
            reportEntries = Array.Empty<CardReportEntry>();
            reportDeckName = string.Empty;
            deleteLogEntries.Clear();
            showDeleteLog = true;

            try
            {
                var deck = await DbHelper.GetDeckAsync(deckId);
                var deckName = deck?.Name ?? deckId;
                await LogDeleteMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.DataInfoDeleteDeckLogStartingFormat, deckName));

                var cards = await DbHelper.GetCardsByDeckAsync(deckId);
                await LogDeleteMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.DataInfoDeleteDeckLogCardCountFormat, cards.Count));

                await LogDeleteMessageAsync(DisplayTexts.DataInfoDeleteDeckLogDeletingDeck);
                await DbHelper.DeleteDeckAsync(deckId);
                await LogDeleteMessageAsync(DisplayTexts.DataInfoDeleteDeckLogDeckDeleted);

                await LogDeleteMessageAsync(DisplayTexts.DataInfoDeleteDeckLogReloadingDecks);
                await LoadDecksAsync();

                await LogDeleteMessageAsync(DisplayTexts.DataInfoDeleteDeckLogFinished);
            }
            catch (Exception ex)
            {
                await LogDeleteMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.DataInfoDeleteDeckLogErrorFormat, ex.Message));
            }
            finally
            {
                isDeletingDeck = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task LoadDecksAsync()
        {
            isLoadingDecks = true;

            try
            {
                var loadedDecks = await DbHelper.GetAllDecksAsync();

                decks = loadedDecks
                    .OrderBy(deck => deck.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(deck => deck.Id, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                if (decks.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(selectedDeckId) || !decks.Any(deck => deck.Id == selectedDeckId))
                    {
                        selectedDeckId = decks[0].Id;
                    }
                }
                else
                {
                    selectedDeckId = null;
                }
            }
            finally
            {
                isLoadingDecks = false;
                showReport = false;
                reportEntries = Array.Empty<CardReportEntry>();
                reportDeckName = string.Empty;
            }
        }

        private async Task LogDeleteMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            deleteLogEntries.Add(message);
            showDeleteLog = true;
            await InvokeAsync(StateHasChanged);
        }

        private async Task ShowReportAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedDeckId))
            {
                return;
            }

            isLoadingReport = true;
            showReport = false;
            reportEntries = Array.Empty<CardReportEntry>();
            reportDeckName = string.Empty;

            try
            {
                var deck = await DbHelper.GetDeckAsync(selectedDeckId);
                reportDeckName = deck?.Name ?? selectedDeckId;

                var cards = await DbHelper.GetCardsByDeckAsync(selectedDeckId);

                reportEntries = cards
                    .OrderBy(card => card.Id, StringComparer.CurrentCultureIgnoreCase)
                    .Select(card => new CardReportEntry(
                        card.Id,
                        card.Image.Length,
                        card.Description.Length))
                    .ToList();

                showReport = true;
            }
            finally
            {
                isLoadingReport = false;
            }
        }
    }
}

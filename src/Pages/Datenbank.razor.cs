using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Toolbox.Components;
using Toolbox.Helpers;
using Toolbox.Layout;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Pages
{
    public partial class Datenbank
    {
        [CascadingParameter]
        private MainLayout? Layout { get; set; }

        protected override void OnInitialized()
        {
            LogService.LogDebug($"Seite '{DisplayTexts.DatabasePageTitle}' initialisiert.");
            Layout?.UpdateCurrentPageTitle(DisplayTexts.DatabasePageTitle);
        }

        protected override async Task OnInitializedAsync()
        {
            await DbHelper.InitializeAsync();
            await LoadDecksAsync();
        }

        private const string DefaultDescriptionText = "Keine Beschreibung vorhanden";
        private const long MaxZipFileSize = 50 * 1024 * 1024;
        private const int StreamBufferSize = 64 * 1024;

        private bool isImporting;
        private List<string> importLogEntries = new();
        private string importReportDeckName = string.Empty;
        private List<CardReportEntry> importReportEntries = new();
        private bool showImportLog;
        private bool showImportReport;
        private string importStatusCssClass = string.Empty;
        private string? importStatusMessage;

        private IReadOnlyList<Deck> decks = Array.Empty<Deck>();
        private List<string> deleteLogEntries = new();
        private bool isDeletingDeck;
        private bool isLoadingDecks;
        private bool isLoadingReport;
        private string dataReportDeckName = string.Empty;
        private IReadOnlyList<CardReportEntry> dataReportEntries = Array.Empty<CardReportEntry>();
        private string? selectedDeckId;
        private bool showDeleteLog;
        private bool showDataReport;

        private HelpDialog? helpDialog;

        private bool CanDeleteDeck => !string.IsNullOrWhiteSpace(selectedDeckId) && !isLoadingDecks && !isDeletingDeck && !isImporting;

        private bool CanShowReport => !string.IsNullOrWhiteSpace(selectedDeckId) && !isLoadingDecks && !isLoadingReport && !isDeletingDeck && !isImporting;

        private string GetHelpButtonLabel(string controlLabel) => HelpDialog.GetButtonLabel(controlLabel);

        private Task ShowHelpAsync(string helpKey, string helpTitle) => helpDialog?.ShowAsync(helpKey, helpTitle) ?? Task.CompletedTask;

        private void CloseImportLog()
        {
            if (isImporting)
            {
                return;
            }

            showImportLog = false;
        }

        private void CloseImportReport()
        {
            showImportReport = false;
        }

        private Task CloseDataReport()
        {
            showDataReport = false;
            dataReportEntries = Array.Empty<CardReportEntry>();
            dataReportDeckName = string.Empty;
            return Task.CompletedTask;
        }

        private async Task HandleFileSelectedAsync(InputFileChangeEventArgs args)
        {
            importStatusMessage = null;
            importStatusCssClass = string.Empty;
            showImportLog = false;
            showImportReport = false;
            importReportEntries.Clear();
            importLogEntries.Clear();

            var file = args.File;

            if (file is null)
            {
                return;
            }

            if (!file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                importStatusMessage = DisplayTexts.ImportExportDeckInvalidFileType;
                importStatusCssClass = "text-danger";
                return;
            }

            if (file.Size > MaxZipFileSize)
            {
                importStatusMessage = DisplayTexts.ImportExportDeckFileTooLarge;
                importStatusCssClass = "text-danger";
                return;
            }

            isImporting = true;
            importStatusMessage = DisplayTexts.ImportExportDeckImportInProgress;
            importStatusCssClass = "text-muted";
            showImportLog = true;
            StateHasChanged();

            try
            {
                var importedDeckId = await ImportDeckAsync(file);
                await ShowImportReportAsync(importedDeckId);
                importStatusMessage = DisplayTexts.ImportExportDeckImportSuccess;
                importStatusCssClass = "text-success";
                await LogImportMessageAsync(importStatusMessage);
                await LoadDecksAsync(importedDeckId);
            }
            catch (Exception exception)
            {
                importStatusMessage = string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckImportFailedFormat, exception.Message);
                importStatusCssClass = "text-danger";
                await LogImportMessageAsync(importStatusMessage);
            }
            finally
            {
                isImporting = false;
                StateHasChanged();
            }
        }

        private async Task<string> ImportDeckAsync(IBrowserFile file)
        {
            var deckId = Path.GetFileNameWithoutExtension(file.Name);

            if (string.IsNullOrWhiteSpace(deckId))
            {
                await LogImportMessageAsync(DisplayTexts.ImportExportDeckMissingDeckName);
                throw new InvalidDataException(DisplayTexts.ImportExportDeckMissingDeckName);
            }

            await LogImportMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckLogStartingFormat, deckId));

            await using var browserFileStream = file.OpenReadStream(MaxZipFileSize);
            var memoryStreamCapacity = (int)Math.Min(file.Size, int.MaxValue);
            using var memoryStream = new MemoryStream(memoryStreamCapacity);
            await browserFileStream.CopyToAsync(memoryStream, StreamBufferSize);
            memoryStream.Position = 0;

            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false);

            if (archive.Entries.Count == 0)
            {
                await LogImportMessageAsync(DisplayTexts.ImportExportDeckArchiveEmpty);
                throw new InvalidDataException(DisplayTexts.ImportExportDeckArchiveEmpty);
            }

            var cards = new Dictionary<string, CardImportData>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = Path.GetFileName(entry.FullName);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var cardName = Path.GetFileNameWithoutExtension(fileName);

                if (string.IsNullOrWhiteSpace(cardName))
                {
                    continue;
                }

                if (!cards.TryGetValue(cardName, out var importData) || importData is null)
                {
                    importData = new CardImportData();
                }

                if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    importData.Image = await ReadEntryBytesAsync(entry);
                    await LogImportMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckLogFileImportedFormat, entry.FullName));
                }
                else if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    importData.Description = await ReadEntryTextAsync(entry);
                    await LogImportMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckLogFileImportedFormat, entry.FullName));
                }
                else
                {
                    continue;
                }

                cards[cardName] = importData;
            }

            if (cards.Count == 0)
            {
                await LogImportMessageAsync(DisplayTexts.ImportExportDeckNoCardsFound);
                throw new InvalidDataException(DisplayTexts.ImportExportDeckNoCardsFound);
            }

            foreach (var (cardName, data) in cards)
            {
                if (data.Image is null)
                {
                    await LogImportMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckMissingImageFormat, cardName));
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckMissingImageFormat, cardName));
                }

                if (string.IsNullOrWhiteSpace(data.Description))
                {
                    data.Description = DefaultDescriptionText;
                    await LogImportMessageAsync(string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckLogDescriptionFallbackFormat, cardName));
                }
            }

            var existingDeck = await DbHelper.GetDeckAsync(deckId);

            if (existingDeck is not null)
            {
                await LogImportMessageAsync(DisplayTexts.ImportExportDeckLogExistingDeckFound);
                await DbHelper.DeleteDeckAsync(deckId);
                await LogImportMessageAsync(DisplayTexts.ImportExportDeckLogDeckDeleted);
            }

            await DbHelper.CreateDeckAsync(new Deck
            {
                Id = deckId,
                Name = deckId
            });

            await LogImportMessageAsync(DisplayTexts.ImportExportDeckLogDeckCreated);

            var importedCards = cards
                .Select(pair => new Spielkarte
                {
                    Id = pair.Key,
                    DeckId = deckId,
                    Image = pair.Value.Image!,
                    Description = pair.Value.Description!,
                })
                .ToList();

            await DbHelper.CreateCardsAsync(importedCards);

            await LogImportMessageAsync(DisplayTexts.ImportExportDeckLogFinished);

            return deckId;
        }

        private async Task LogImportMessageAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            importLogEntries.Add(message);
            await InvokeAsync(StateHasChanged);
        }

        private async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry)
        {
            await using var entryStream = entry.Open();
            using var memoryStream = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));
            var buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);

            try
            {
                int bytesRead;
                while ((bytesRead = await entryStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await memoryStream.WriteAsync(buffer, 0, bytesRead);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return memoryStream.ToArray();
        }

        private async Task<string> ReadEntryTextAsync(ZipArchiveEntry entry)
        {
            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: StreamBufferSize, leaveOpen: false);
            return await reader.ReadToEndAsync();
        }

        private async Task ShowImportReportAsync(string deckId)
        {
            var deck = await DbHelper.GetDeckAsync(deckId);
            importReportDeckName = deck?.Name ?? deckId;

            var cards = await DbHelper.GetCardsByDeckAsync(deckId);

            importReportEntries = cards
                .OrderBy(card => card.Id, StringComparer.CurrentCultureIgnoreCase)
                .Select(card => new CardReportEntry(
                    card.Id,
                    card.Image.Length,
                    card.Description.Length))
                .ToList();

            showImportReport = true;
        }

        private async Task DeleteSelectedDeckAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedDeckId) || isDeletingDeck)
            {
                return;
            }

            var deckId = selectedDeckId;
            isDeletingDeck = true;
            showDataReport = false;
            dataReportEntries = Array.Empty<CardReportEntry>();
            dataReportDeckName = string.Empty;
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

        private async Task LoadDecksAsync(string? deckIdToSelect = null)
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
                    var preferredDeckId = deckIdToSelect ?? selectedDeckId;

                    if (string.IsNullOrWhiteSpace(preferredDeckId) || !decks.Any(deck => deck.Id == preferredDeckId))
                    {
                        selectedDeckId = decks[0].Id;
                    }
                    else
                    {
                        selectedDeckId = preferredDeckId;
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
                showDataReport = false;
                dataReportEntries = Array.Empty<CardReportEntry>();
                dataReportDeckName = string.Empty;
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
            showDataReport = false;
            dataReportEntries = Array.Empty<CardReportEntry>();
            dataReportDeckName = string.Empty;

            try
            {
                var deck = await DbHelper.GetDeckAsync(selectedDeckId);
                dataReportDeckName = deck?.Name ?? selectedDeckId;

                var cards = await DbHelper.GetCardsByDeckAsync(selectedDeckId);

                dataReportEntries = cards
                    .OrderBy(card => card.Id, StringComparer.CurrentCultureIgnoreCase)
                    .Select(card => new CardReportEntry(
                        card.Id,
                        card.Image.Length,
                        card.Description.Length))
                    .ToList();

                showDataReport = true;
            }
            finally
            {
                isLoadingReport = false;
            }
        }

        private sealed class CardImportData
        {
            public string? Description { get; set; }

            public byte[]? Image { get; set; }
        }
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Toolbox.Layout;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Pages
{
    public partial class ImportExport
    {
        [CascadingParameter]
        private MainLayout? Layout { get; set; }

        protected override void OnInitialized()
        {
            Layout?.UpdateCurrentPageTitle(DisplayTexts.ImportExportPageTitle);
        }

        protected override async Task OnInitializedAsync()
        {
            await DbHelper.InitializeAsync();
        }

        private const string DefaultDescriptionText = "Keine Beschreibung vorhanden";
        private const long MaxZipFileSize = 50 * 1024 * 1024;
        private const int StreamBufferSize = 64 * 1024;
        private bool isImporting;
        private List<string> logEntries = new();
        private string reportDeckName = string.Empty;
        private List<CardReportEntry> reportEntries = new();
        private bool showImportLog;
        private bool showImportReport;
        private string statusCssClass = string.Empty;
        private string? statusMessage;

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

        private async Task HandleFileSelectedAsync(InputFileChangeEventArgs args)
        {
            statusMessage = null;
            statusCssClass = string.Empty;
            showImportLog = false;
            showImportReport = false;
            reportEntries.Clear();
            logEntries.Clear();

            var file = args.File;

            if (file is null)
            {
                return;
            }

            if (!file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                statusMessage = DisplayTexts.ImportExportDeckInvalidFileType;
                statusCssClass = "text-danger";
                return;
            }

            if (file.Size > MaxZipFileSize)
            {
                statusMessage = DisplayTexts.ImportExportDeckFileTooLarge;
                statusCssClass = "text-danger";
                return;
            }

            isImporting = true;
            statusMessage = DisplayTexts.ImportExportDeckImportInProgress;
            statusCssClass = "text-muted";
            showImportLog = true;
            StateHasChanged();

            try
            {
                var importedDeckId = await ImportDeckAsync(file);
                await ShowImportReportAsync(importedDeckId);
                statusMessage = DisplayTexts.ImportExportDeckImportSuccess;
                statusCssClass = "text-success";
                await LogImportMessageAsync(statusMessage);
            }
            catch (Exception exception)
            {
                statusMessage = string.Format(CultureInfo.CurrentCulture, DisplayTexts.ImportExportDeckImportFailedFormat, exception.Message);
                statusCssClass = "text-danger";
                await LogImportMessageAsync(statusMessage);
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
                    Description = pair.Value.Description!
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

            logEntries.Add(message);
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
            reportDeckName = deck?.Name ?? deckId;

            var cards = await DbHelper.GetCardsByDeckAsync(deckId);

            reportEntries = cards
                .OrderBy(card => card.Id, StringComparer.CurrentCultureIgnoreCase)
                .Select(card => new CardReportEntry(
                    card.Id,
                    card.Image.Length,
                    card.Description.Length))
                .ToList();

            showImportReport = true;
        }

        private sealed class CardImportData
        {
            public string? Description
            {
                get; set;
            }

            public byte[]? Image
            {
                get; set;
            }
        }
    }
}

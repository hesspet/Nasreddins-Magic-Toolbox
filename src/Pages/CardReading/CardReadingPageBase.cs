using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Toolbox.Layout;
using Toolbox.Models;
using Toolbox.Resources;

namespace Toolbox.Pages.CardReading;

public abstract class CardReadingPageBase : ComponentBase
{
    private DeckOption? selectedDeck;

    [CascadingParameter]
    protected MainLayout? Layout { get; set; }

    protected DeckOption? SelectedDeck => selectedDeck;

    protected string SelectedDeckId => selectedDeck?.DeckId ?? string.Empty;

    protected string SelectedDeckDisplayName => selectedDeck?.DisplayName ?? string.Empty;

    protected abstract string PageTitle { get; }

    protected abstract string DeckStorageKey { get; }

    protected virtual string CardPlaceholderText => DisplayTexts.CardSearchPickerPlaceholder;

    protected override void OnInitialized()
    {
        Layout?.UpdateCurrentPageTitle(PageTitle);
    }

    protected virtual Task OnDeckChangedAsync(DeckOption? option) => Task.CompletedTask;

    protected async Task HandleDeckSelectionChangedAsync(DeckOption? option)
    {
        selectedDeck = option;
        await OnDeckChangedAsync(option);
        await InvokeAsync(StateHasChanged);
    }
}

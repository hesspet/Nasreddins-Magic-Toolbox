using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Toolbox.Models;

namespace Toolbox.Helpers;

/// <summary>
/// Provides CRUD access to IndexedDB for <see cref="Deck"/> and <see cref="Spielkarte"/> entities.
/// </summary>
public sealed class IndexedDbHelper : IAsyncDisposable
{
    private readonly Lazy<ValueTask<IJSObjectReference>> _moduleTask;

    public IndexedDbHelper(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/indexedDbHelper.js"));
    }

    /// <summary>
    /// Ensures the IndexedDB database and object stores exist.
    /// </summary>
    public async Task InitializeAsync()
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("initialize");
    }

    /// <summary>
    /// Creates a new deck entry.
    /// </summary>
    public async Task CreateDeckAsync(Deck deck)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("createDeck", deck);
    }

    /// <summary>
    /// Retrieves a deck by its identifier.
    /// </summary>
    public async Task<Deck?> GetDeckAsync(string id)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<Deck?>("getDeck", id);
    }

    /// <summary>
    /// Retrieves all decks.
    /// </summary>
    public async Task<IReadOnlyList<Deck>> GetAllDecksAsync()
    {
        var module = await _moduleTask.Value;
        var result = await module.InvokeAsync<List<Deck>>("getAllDecks");
        return result;
    }

    /// <summary>
    /// Updates an existing deck.
    /// </summary>
    public async Task UpdateDeckAsync(Deck deck)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("updateDeck", deck);
    }

    /// <summary>
    /// Deletes a deck and all dependent cards.
    /// </summary>
    public async Task DeleteDeckAsync(string id)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("deleteDeck", id);
    }

    /// <summary>
    /// Creates a new card entry.
    /// </summary>
    public async Task CreateCardAsync(Spielkarte card)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("createCard", card);
    }

    /// <summary>
    /// Retrieves a card by its surrogate row identifier.
    /// </summary>
    public async Task<Spielkarte?> GetCardAsync(string rowId)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<Spielkarte?>("getCard", rowId);
    }

    /// <summary>
    /// Retrieves all cards.
    /// </summary>
    public async Task<IReadOnlyList<Spielkarte>> GetAllCardsAsync()
    {
        var module = await _moduleTask.Value;
        var result = await module.InvokeAsync<List<Spielkarte>>("getAllCards");
        return result;
    }

    /// <summary>
    /// Retrieves all cards belonging to the specified deck.
    /// </summary>
    public async Task<IReadOnlyList<Spielkarte>> GetCardsByDeckAsync(string deckId)
    {
        var module = await _moduleTask.Value;
        var result = await module.InvokeAsync<List<Spielkarte>>("getCardsByDeck", deckId);
        return result;
    }

    /// <summary>
    /// Updates an existing card entry.
    /// </summary>
    public async Task UpdateCardAsync(Spielkarte card)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("updateCard", card);
    }

    /// <summary>
    /// Deletes a card by its surrogate row identifier.
    /// </summary>
    public async Task DeleteCardAsync(string rowId)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("deleteCard", rowId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}

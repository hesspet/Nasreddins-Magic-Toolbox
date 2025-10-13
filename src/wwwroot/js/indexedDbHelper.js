const DB_NAME = 'MagicToolboxDb';
const DB_VERSION = 2;
const DECK_STORE = 'decks';
const CARD_STORE = 'cards';
const CARD_DECK_INDEX = 'cardsByDeck';
const CARD_ID_INDEX = 'cardsById';
const CARD_DECK_ID_INDEX = 'cardsByDeckAndId';
const CARD_KEY_PATH = 'key';

let dbPromise = null;

function buildErrorMessage(error, defaultMessage) {
    if (!error) {
        return defaultMessage;
    }

    if (typeof error === 'string') {
        return error || defaultMessage;
    }

    if (typeof error === 'object') {
        const hasMessage = typeof error.message === 'string' && error.message;
        if (hasMessage) {
            return error.message;
        }

        const hasName = typeof error.name === 'string' && error.name;
        if (hasName) {
            return `${error.name}: ${defaultMessage}`;
        }
    }

    return defaultMessage;
}

function createError(error, defaultMessage) {
    const message = buildErrorMessage(error, defaultMessage);

    if (error instanceof Error && error.message === message) {
        return error;
    }

    return new Error(message);
}

function rejectWithError(reject, defaultMessage) {
    return event => {
        const target = event?.target;
        const error = target?.error ?? target?.transaction?.error ?? event?.error ?? null;
        reject(createError(error, defaultMessage));
    };
}

function createCardStore(db) {
    const cardStore = db.createObjectStore(CARD_STORE, { keyPath: CARD_KEY_PATH });
    cardStore.createIndex(CARD_DECK_INDEX, 'deckId', { unique: false });
    cardStore.createIndex(CARD_ID_INDEX, 'id', { unique: false });
    cardStore.createIndex(CARD_DECK_ID_INDEX, ['deckId', 'id'], { unique: true });
    return cardStore;
}

function ensureCardStoreIndexes(cardStore) {
    if (!cardStore.indexNames.contains(CARD_DECK_INDEX)) {
        cardStore.createIndex(CARD_DECK_INDEX, 'deckId', { unique: false });
    }

    if (!cardStore.indexNames.contains(CARD_ID_INDEX)) {
        cardStore.createIndex(CARD_ID_INDEX, 'id', { unique: false });
    }

    if (!cardStore.indexNames.contains(CARD_DECK_ID_INDEX)) {
        cardStore.createIndex(CARD_DECK_ID_INDEX, ['deckId', 'id'], { unique: true });
    }
}

function openDatabase() {
    if (!dbPromise) {
        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onupgradeneeded = event => {
                const db = event.target.result;
                const transaction = event.target.transaction;

                if (!db.objectStoreNames.contains(DECK_STORE)) {
                    db.createObjectStore(DECK_STORE, { keyPath: 'id' });
                }

                const oldVersion = event.oldVersion ?? 0;

                if (!db.objectStoreNames.contains(CARD_STORE)) {
                    createCardStore(db);
                } else if (oldVersion < 2) {
                    const legacyStore = transaction.objectStore(CARD_STORE);
                    const getAllRequest = legacyStore.getAll();

                    getAllRequest.onsuccess = () => {
                        const legacyCards = getAllRequest.result ?? [];
                        db.deleteObjectStore(CARD_STORE);
                        const newStore = createCardStore(db);

                        legacyCards.forEach(card => {
                            try {
                                const upgraded = upgradeLegacyCard(card);
                                newStore.add(upgraded);
                            } catch (error) {
                                console.error('Failed to upgrade legacy card entry.', error);
                            }
                        });
                    };

                    getAllRequest.onerror = eventError => {
                        const upgradeError = eventError?.target?.error ?? eventError?.error ?? null;
                        console.error('Failed to read legacy card data during upgrade.', upgradeError);
                    };
                } else {
                    const cardStore = transaction.objectStore(CARD_STORE);
                    ensureCardStoreIndexes(cardStore);
                }
            };

            request.onsuccess = event => resolve(event.target.result);
            request.onerror = rejectWithError(reject, 'Opening IndexedDB failed.');
        });
    }

    return dbPromise;
}

function requestToPromise(request, mapper) {
    return new Promise((resolve, reject) => {
        request.onsuccess = () => {
            const value = mapper ? mapper(request.result) : request.result;
            resolve(value);
        };
        request.onerror = rejectWithError(reject, 'IndexedDB request failed.');
    });
}

function normalizeDeck(deck) {
    if (!deck || typeof deck.id !== 'string') {
        throw new Error('Deck requires an id.');
    }

    return {
        id: deck.id,
        name: deck.name ?? ''
    };
}

function base64ToArrayBuffer(base64) {
    if (!base64) {
        return null;
    }

    const binary = atob(base64);
    const length = binary.length;
    const bytes = new Uint8Array(length);
    for (let i = 0; i < length; i += 1) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
}

function arrayBufferToBase64(buffer) {
    if (!buffer) {
        return '';
    }

    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.length; i += 1) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

function normalizeCard(card) {
    if (!card || typeof card.id !== 'string') {
        throw new Error('Spielkarte requires an id.');
    }

    if (!card.deckId || typeof card.deckId !== 'string') {
        throw new Error('Spielkarte requires a deckId.');
    }

    const normalizedImage = typeof card.image === 'string' ? base64ToArrayBuffer(card.image) : card.image ?? null;
    const key = buildCardKey(card.deckId, card.id);

    return {
        [CARD_KEY_PATH]: key,
        id: card.id,
        deckId: card.deckId,
        description: card.description ?? '',
        image: normalizedImage
    };
}

function buildCardKey(deckId, cardId) {
    if (!deckId || typeof deckId !== 'string') {
        throw new Error('Spielkarte requires a deckId.');
    }

    if (!cardId || typeof cardId !== 'string') {
        throw new Error('Spielkarte requires an id.');
    }

    return `${deckId}::${cardId}`;
}

function mapDeck(deck) {
    if (!deck) {
        return null;
    }

    return {
        id: deck.id,
        name: deck.name ?? ''
    };
}

function mapCard(card) {
    if (!card) {
        return null;
    }

    return {
        id: card.id,
        deckId: card.deckId,
        description: card.description ?? '',
        image: arrayBufferToBase64(card.image)
    };
}

export async function initialize() {
    await openDatabase();
}

export async function createDeck(deck) {
    const normalized = normalizeDeck(deck);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(DECK_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');
        transaction.objectStore(DECK_STORE).add(normalized);
    });
}

export async function getDeck(id) {
    const db = await openDatabase();
    const transaction = db.transaction(DECK_STORE, 'readonly');
    const store = transaction.objectStore(DECK_STORE);
    return requestToPromise(store.get(id), mapDeck);
}

export async function getAllDecks() {
    const db = await openDatabase();
    const transaction = db.transaction(DECK_STORE, 'readonly');
    const store = transaction.objectStore(DECK_STORE);
    return requestToPromise(store.getAll(), result => result.map(mapDeck));
}

export async function updateDeck(deck) {
    const normalized = normalizeDeck(deck);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(DECK_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');
        transaction.objectStore(DECK_STORE).put(normalized);
    });
}

export async function deleteDeck(id) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction([DECK_STORE, CARD_STORE], 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');

        const deckStore = transaction.objectStore(DECK_STORE);
        const cardStore = transaction.objectStore(CARD_STORE);

        deckStore.delete(id);

        const index = cardStore.index(CARD_DECK_INDEX);
        const range = IDBKeyRange.only(id);
        const cursorRequest = index.openKeyCursor(range);
        cursorRequest.onsuccess = event => {
            const cursor = event.target.result;
            if (cursor) {
                cardStore.delete(cursor.primaryKey);
                cursor.continue();
            }
        };
        cursorRequest.onerror = rejectWithError(reject, 'IndexedDB cursor failed.');
    });
}

export async function createCard(card) {
    const normalized = normalizeCard(card);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');
        transaction.objectStore(CARD_STORE).add(normalized);
    });
}

export async function getCard(deckId, id) {
    if (!deckId || typeof deckId !== 'string') {
        throw new Error('Spielkarte requires a deckId.');
    }

    if (!id || typeof id !== 'string') {
        throw new Error('Spielkarte requires an id.');
    }

    const db = await openDatabase();
    const transaction = db.transaction(CARD_STORE, 'readonly');
    const store = transaction.objectStore(CARD_STORE);
    const index = store.index(CARD_DECK_ID_INDEX);
    const key = [deckId, id];
    return requestToPromise(index.get(key), mapCard);
}

export async function getAllCards() {
    const db = await openDatabase();
    const transaction = db.transaction(CARD_STORE, 'readonly');
    const store = transaction.objectStore(CARD_STORE);
    return requestToPromise(store.getAll(), result => result.map(mapCard));
}

export async function getCardsByDeck(deckId) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readonly');
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');

        const store = transaction.objectStore(CARD_STORE);
        const index = store.index(CARD_DECK_INDEX);
        const request = index.getAll(deckId);

        request.onsuccess = () => resolve(request.result.map(mapCard));
        request.onerror = rejectWithError(reject, 'IndexedDB request failed.');
    });
}

export async function updateCard(card) {
    const normalized = normalizeCard(card);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');
        transaction.objectStore(CARD_STORE).put(normalized);
    });
}

export async function deleteCard(deckId, id) {
    if (!deckId || typeof deckId !== 'string') {
        throw new Error('Spielkarte requires a deckId.');
    }

    if (!id || typeof id !== 'string') {
        throw new Error('Spielkarte requires an id.');
    }

    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = rejectWithError(reject, 'IndexedDB transaction failed.');
        const key = buildCardKey(deckId, id);
        transaction.objectStore(CARD_STORE).delete(key);
    });
}

function upgradeLegacyCard(card) {
    if (!card) {
        throw new Error('Legacy card entry is invalid.');
    }

    const deckId = card.deckId;
    const id = card.id;
    const key = buildCardKey(deckId, id);

    return {
        [CARD_KEY_PATH]: key,
        id,
        deckId,
        description: card.description ?? '',
        image: card.image ?? null
    };
}

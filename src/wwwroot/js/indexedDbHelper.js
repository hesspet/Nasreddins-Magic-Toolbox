const DB_NAME = 'MagicToolboxDb';
const DB_VERSION = 1;
const DECK_STORE = 'decks';
const CARD_STORE = 'cards';
const CARD_DECK_INDEX = 'cardsByDeck';

let dbPromise = null;

function openDatabase() {
    if (!dbPromise) {
        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(DB_NAME, DB_VERSION);

            request.onupgradeneeded = event => {
                const db = event.target.result;

                if (!db.objectStoreNames.contains(DECK_STORE)) {
                    db.createObjectStore(DECK_STORE, { keyPath: 'id' });
                }

                if (!db.objectStoreNames.contains(CARD_STORE)) {
                    const cardStore = db.createObjectStore(CARD_STORE, { keyPath: 'id' });
                    cardStore.createIndex(CARD_DECK_INDEX, 'deckId', { unique: false });
                }
            };

            request.onsuccess = event => resolve(event.target.result);
            request.onerror = () => reject(request.error);
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
        request.onerror = () => reject(request.error);
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

    return {
        id: card.id,
        deckId: card.deckId,
        description: card.description ?? '',
        image: normalizedImage
    };
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
        transaction.onerror = () => reject(transaction.error);
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
        transaction.onerror = () => reject(transaction.error);
        transaction.objectStore(DECK_STORE).put(normalized);
    });
}

export async function deleteDeck(id) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction([DECK_STORE, CARD_STORE], 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);

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
        cursorRequest.onerror = () => reject(cursorRequest.error);
    });
}

export async function createCard(card) {
    const normalized = normalizeCard(card);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.objectStore(CARD_STORE).add(normalized);
    });
}

export async function getCard(id) {
    const db = await openDatabase();
    const transaction = db.transaction(CARD_STORE, 'readonly');
    const store = transaction.objectStore(CARD_STORE);
    return requestToPromise(store.get(id), mapCard);
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
        transaction.onerror = () => reject(transaction.error);

        const store = transaction.objectStore(CARD_STORE);
        const index = store.index(CARD_DECK_INDEX);
        const request = index.getAll(deckId);

        request.onsuccess = () => resolve(request.result.map(mapCard));
        request.onerror = () => reject(request.error);
    });
}

export async function updateCard(card) {
    const normalized = normalizeCard(card);
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.objectStore(CARD_STORE).put(normalized);
    });
}

export async function deleteCard(id) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(CARD_STORE, 'readwrite');
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
        transaction.objectStore(CARD_STORE).delete(id);
    });
}

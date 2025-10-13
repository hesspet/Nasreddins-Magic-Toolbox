const cardObservers = new WeakMap();

function ensureObserver(cardElement) {
    const existing = cardObservers.get(cardElement);
    if (existing) {
        existing.disconnect();
    }

    const observer = new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (entry.target !== cardElement) {
                return;
            }

            if (entry.intersectionRatio < 0.5) {
                cardElement.classList.add('tarot-card--compact');
            } else {
                cardElement.classList.remove('tarot-card--compact');
            }
        });
    }, {
        threshold: [0.5]
    });

    observer.observe(cardElement);
    cardObservers.set(cardElement, observer);
}

export function observeCardVisibility(cardElement) {
    if (!cardElement) {
        return;
    }

    ensureObserver(cardElement);
}

export function disconnectCardVisibility(cardElement) {
    if (!cardElement) {
        return;
    }

    const observer = cardObservers.get(cardElement);
    if (observer) {
        observer.disconnect();
        cardObservers.delete(cardElement);
    }

    cardElement.classList.remove('tarot-card--compact');
}

export function scrollToDescription(descriptionElement) {
    if (!descriptionElement) {
        return;
    }

    descriptionElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

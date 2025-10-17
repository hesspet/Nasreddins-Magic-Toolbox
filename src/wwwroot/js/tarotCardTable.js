const observers = new WeakMap();

export function observeCardTable(element, dotnetRef) {
    if (!element) {
        return;
    }

    const existing = observers.get(element);
    if (existing) {
        existing.observer.disconnect();
    }

    const observer = new ResizeObserver(entries => {
        for (const entry of entries) {
            const { width, height } = entry.contentRect;
            dotnetRef.invokeMethodAsync('UpdateDimensions', width, height);
        }
    });

    observer.observe(element);
    observers.set(element, { observer });
}

export function disconnectCardTable(element) {
    const existing = observers.get(element);
    if (!existing) {
        return;
    }

    existing.observer.disconnect();
    observers.delete(element);
}

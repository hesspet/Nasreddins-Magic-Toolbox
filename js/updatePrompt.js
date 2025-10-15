(function () {
    if (!('serviceWorker' in navigator)) {
        return;
    }

    let hasControllerChanged = false;
    const updateSettingKey = 'CheckForUpdatesOnStartup';

    function shouldCheckForUpdatesOnStartup() {
        try {
            const storedValue = window.localStorage.getItem(updateSettingKey);

            if (storedValue === null || storedValue === undefined) {
                return true;
            }

            const parsedValue = JSON.parse(storedValue);
            return parsedValue !== false;
        } catch (error) {
            console.warn('Konnte Einstellung für die Updateprüfung nicht auslesen:', error);
            return true;
        }
    }

    function promptUserForUpdate(registration) {
        const waitingWorker = registration.waiting;

        if (!waitingWorker) {
            return;
        }

        const wantsUpdate = window.confirm('Es ist ein neues Update verfügbar. Möchtest du es jetzt installieren?');
        if (wantsUpdate) {
            waitingWorker.postMessage({ type: 'SKIP_WAITING' });
        }
    }

    const shouldCheckForUpdates = shouldCheckForUpdatesOnStartup();

    navigator.serviceWorker
        .register('service-worker.js')
        .then(registration => {
            if (!shouldCheckForUpdates) {
                return;
            }

            if (registration.waiting) {
                promptUserForUpdate(registration);
            }

            registration.addEventListener('updatefound', () => {
                const newWorker = registration.installing;
                if (!newWorker) {
                    return;
                }

                newWorker.addEventListener('statechange', () => {
                    if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                        promptUserForUpdate(registration);
                    }
                });
            });
        })
        .catch(error => console.error('Service Worker Registrierung fehlgeschlagen:', error));

    if (shouldCheckForUpdates) {
        navigator.serviceWorker.addEventListener('controllerchange', () => {
            if (hasControllerChanged) {
                return;
            }

            hasControllerChanged = true;
            window.location.reload();
        });
    }
})();

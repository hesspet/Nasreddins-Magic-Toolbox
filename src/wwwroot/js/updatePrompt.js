(function () {
    if (!('serviceWorker' in navigator)) {
        return;
    }

    let hasControllerChanged = false;
    let latestRegistration = null;

    const updateSettingKey = 'CheckForUpdatesOnStartup';
    const offlineModeKey = 'OfflineModeEnabled';

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

    function isOfflineModeEnabled() {
        try {
            const storedValue = window.localStorage.getItem(offlineModeKey);

            if (storedValue === null || storedValue === undefined) {
                return false;
            }

            const parsedValue = JSON.parse(storedValue);
            if (typeof parsedValue === 'boolean') {
                return parsedValue;
            }

            if (typeof parsedValue === 'string') {
                return parsedValue.toLowerCase() === 'true';
            }

            return false;
        } catch (error) {
            console.warn('Konnte Einstellung für den Offline-Modus nicht auslesen:', error);
            return false;
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

    function tryPostMessage(worker) {
        if (!worker) {
            return;
        }

        try {
            worker.postMessage({ type: 'SET_OFFLINE_MODE', enabled: offlineModeEnabled });
        } catch (error) {
            console.warn('Konnte Offline-Status nicht an Service Worker senden:', error);
        }
    }

    function notifyServiceWorkerAboutOfflineMode() {
        tryPostMessage(navigator.serviceWorker.controller);

        if (latestRegistration) {
            tryPostMessage(latestRegistration.installing);
            tryPostMessage(latestRegistration.waiting);
            tryPostMessage(latestRegistration.active);
        }
    }

    function setOfflineMode(enabled) {
        const isEnabled = enabled === true || enabled === 'true';
        const wasEnabled = offlineModeEnabled;

        offlineModeEnabled = isEnabled;
        notifyServiceWorkerAboutOfflineMode();

        if (!offlineModeEnabled && wasEnabled && wantsUpdateCheck && latestRegistration && latestRegistration.waiting) {
            promptUserForUpdate(latestRegistration);
        }
    }

    const wantsUpdateCheck = shouldCheckForUpdatesOnStartup();
    let offlineModeEnabled = isOfflineModeEnabled();

    window.nasreddinsMagicToolbox = window.nasreddinsMagicToolbox || {};
    window.nasreddinsMagicToolbox.setOfflineMode = setOfflineMode;

    notifyServiceWorkerAboutOfflineMode();

    navigator.serviceWorker
        .register('service-worker.js')
        .then(registration => {
            latestRegistration = registration;
            notifyServiceWorkerAboutOfflineMode();

            if (wantsUpdateCheck && !offlineModeEnabled && registration.waiting) {
                promptUserForUpdate(registration);
            }

            if (!wantsUpdateCheck) {
                return;
            }

            registration.addEventListener('updatefound', () => {
                const newWorker = registration.installing;
                if (!newWorker) {
                    return;
                }

                newWorker.addEventListener('statechange', () => {
                    if (newWorker.state === 'installed' && navigator.serviceWorker.controller && wantsUpdateCheck && !offlineModeEnabled) {
                        promptUserForUpdate(registration);
                    }
                });
            });
        })
        .catch(error => console.error('Service Worker Registrierung fehlgeschlagen:', error));

    navigator.serviceWorker.ready
        .then(registration => {
            latestRegistration = registration;
            notifyServiceWorkerAboutOfflineMode();
        })
        .catch(() => { });

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        notifyServiceWorkerAboutOfflineMode();

        if (hasControllerChanged) {
            return;
        }

        hasControllerChanged = true;

        if (!wantsUpdateCheck || offlineModeEnabled) {
            return;
        }

        window.location.reload();
    });
})();

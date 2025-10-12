(function () {
    if (!('serviceWorker' in navigator)) {
        return;
    }

    let hasControllerChanged = false;

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

    navigator.serviceWorker
        .register('service-worker.js')
        .then(registration => {
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

    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (hasControllerChanged) {
            return;
        }

        hasControllerChanged = true;
        window.location.reload();
    });
})();

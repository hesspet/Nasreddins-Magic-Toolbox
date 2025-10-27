// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
let offlineModeEnabled = false;

self.addEventListener('fetch', () => { });

self.addEventListener('message', event => {
    const messageType = event?.data?.type;

    if (messageType === 'SKIP_WAITING') {
        self.skipWaiting();
        return;
    }

    if (messageType === 'SET_OFFLINE_MODE') {
        offlineModeEnabled = event?.data?.enabled === true;
    }
});

export function triggerFileInput(elementId) {
    if (!elementId) {
        return;
    }

    const element = document.getElementById(elementId);
    element?.click();
}

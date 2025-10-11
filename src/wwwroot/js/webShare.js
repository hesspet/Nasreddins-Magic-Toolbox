export function isShareSupported() {
    return typeof navigator !== "undefined" && typeof navigator.share === "function";
}

function base64ToUint8Array(base64) {
    const binary = atob(base64);
    const length = binary.length;
    const bytes = new Uint8Array(length);
    for (let i = 0; i < length; i += 1) {
        bytes[i] = binary.charCodeAt(i);
    }

    return bytes;
}

export async function shareDeck(deckName, files) {
    if (!isShareSupported()) {
        throw new Error("Web Share API wird nicht unterstützt.");
    }

    const preparedFiles = Array.isArray(files) ? files : [];
    const shareFiles = [];

    for (const file of preparedFiles) {
        if (!file || !file.FileName || !file.Base64Data) {
            continue;
        }

        const bytes = base64ToUint8Array(file.Base64Data);
        const blob = new Blob([bytes.buffer], { type: file.ContentType || "application/octet-stream" });
        const safeName = file.FileName.replace(/\s+/g, "_");
        shareFiles.push(new File([blob], safeName, { type: blob.type }));
    }

    const shareData = {
        title: deckName || "Tarot Deck",
        text: deckName ? `Tarot-Deck: ${deckName}` : "Tarot-Deck"
    };

    if (shareFiles.length > 0 && navigator.canShare && navigator.canShare({ files: shareFiles })) {
        shareData.files = shareFiles;
    }

    await navigator.share(shareData);
}

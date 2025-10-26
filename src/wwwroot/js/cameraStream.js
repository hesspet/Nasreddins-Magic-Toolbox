const stateMap = new WeakMap();

export async function initialize(videoElement, canvasElement, constraints, captureOptions, fileInputId) {
    if (!videoElement || !canvasElement) {
        return { success: false, errorMessage: "Kameraelemente nicht gefunden." };
    }

    const state = {
        videoElement,
        canvasElement,
        captureOptions,
        fileInputId,
        stream: null
    };

    canvasElement.width = captureOptions?.targetWidth ?? canvasElement.width;
    canvasElement.height = captureOptions?.targetHeight ?? canvasElement.height;

    stateMap.set(videoElement, state);

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        return { success: false, errorMessage: "getUserMedia wird nicht unterstützt." };
    }

    const mediaConstraints = buildConstraints(constraints);

    try {
        const stream = await navigator.mediaDevices.getUserMedia(mediaConstraints);
        state.stream = stream;
        videoElement.srcObject = stream;

        const [track] = stream.getVideoTracks();
        if (track) {
            await applyBestConstraints(track, constraints);
        }

        return { success: true };
    } catch (error) {
        dispose(videoElement);
        return { success: false, errorMessage: error?.message ?? String(error) };
    }
}

export async function captureFrame(videoElement, canvasElement, captureOptions) {
    const state = stateMap.get(videoElement);
    if (!state || !state.stream) {
        throw new Error("Kein aktiver Videostream verfügbar.");
    }

    await waitForMetadata(videoElement);

    const context = canvasElement.getContext("2d", { willReadFrequently: false });
    if (!context) {
        throw new Error("Canvas-Kontext konnte nicht erstellt werden.");
    }

    renderToCanvas(context, videoElement, canvasElement, captureOptions);
    return await canvasToBytes(canvasElement, captureOptions);
}

export async function readFileFromInput(canvasElement, inputId, captureOptions) {
    const input = document.getElementById(inputId);
    if (!input || input.files.length === 0) {
        return new Uint8Array();
    }

    try {
        const file = input.files[0];
        const bitmap = await tryCreateBitmap(file, captureOptions);
        const context = canvasElement.getContext("2d");
        if (!context) {
            throw new Error("Canvas-Kontext konnte nicht erstellt werden.");
        }

        renderBitmapToCanvas(context, bitmap, canvasElement, captureOptions);
        return await canvasToBytes(canvasElement, captureOptions);
    } finally {
        input.value = "";
    }
}

export function triggerFileInput(elementId) {
    const element = document.getElementById(elementId);
    element?.click();
}

export function dispose(videoElement) {
    const state = stateMap.get(videoElement);
    if (!state) {
        return;
    }

    if (state.stream) {
        for (const track of state.stream.getTracks()) {
            track.stop();
        }
    }

    videoElement.srcObject = null;
    stateMap.delete(videoElement);
}

async function applyBestConstraints(track, constraints) {
    if (!track.applyConstraints || typeof track.getCapabilities !== "function") {
        return;
    }

    const capabilities = track.getCapabilities();
    const applied = {};

    if (capabilities.width) {
        applied.width = clampConstraint(capabilities.width, constraints.idealWidth, constraints.maxWidth);
    }

    if (capabilities.height) {
        applied.height = clampConstraint(capabilities.height, constraints.idealHeight, constraints.maxHeight);
    }

    if (capabilities.frameRate) {
        applied.frameRate = clampConstraint(capabilities.frameRate, constraints.idealFrameRate, constraints.idealFrameRate);
    }

    if (Object.keys(applied).length > 0) {
        await track.applyConstraints(applied).catch(() => {});
    }
}

function clampConstraint(range, ideal, maximum) {
    if (!range) {
        return ideal;
    }

    const min = typeof range.min === "number" ? range.min : ideal;
    const maxCap = typeof range.max === "number" ? range.max : maximum ?? range.max;
    const target = typeof ideal === "number" ? ideal : maxCap;
    const limited = Math.min(Math.max(target, min), maxCap);
    return limited;
}

function buildConstraints(constraints) {
    const video = {
        width: buildRange(constraints?.idealWidth, constraints?.maxWidth),
        height: buildRange(constraints?.idealHeight, constraints?.maxHeight),
        frameRate: constraints?.idealFrameRate ? { ideal: constraints.idealFrameRate } : undefined,
        facingMode: constraints?.facingMode ? { ideal: constraints.facingMode } : undefined
    };

    return { video, audio: false };
}

function buildRange(ideal, max) {
    if (ideal == null && max == null) {
        return undefined;
    }

    const range = {};
    if (ideal != null) {
        range.ideal = ideal;
    }

    if (max != null) {
        range.max = max;
    }

    return range;
}

async function waitForMetadata(videoElement) {
    if (videoElement.readyState >= 1) {
        return;
    }

    await new Promise((resolve) => {
        videoElement.addEventListener("loadedmetadata", resolve, { once: true });
    });
}

function renderToCanvas(context, videoElement, canvasElement, captureOptions) {
    const targetWidth = captureOptions?.targetWidth ?? canvasElement.width;
    const targetHeight = captureOptions?.targetHeight ?? canvasElement.height;

    canvasElement.width = targetWidth;
    canvasElement.height = targetHeight;

    const videoWidth = videoElement.videoWidth || targetWidth;
    const videoHeight = videoElement.videoHeight || targetHeight;

    const { sx, sy, sw, sh } = calculateCoverCrop(videoWidth, videoHeight, targetWidth, targetHeight);
    context.drawImage(videoElement, sx, sy, sw, sh, 0, 0, targetWidth, targetHeight);
}

function renderBitmapToCanvas(context, bitmap, canvasElement, captureOptions) {
    const targetWidth = captureOptions?.targetWidth ?? canvasElement.width;
    const targetHeight = captureOptions?.targetHeight ?? canvasElement.height;

    canvasElement.width = targetWidth;
    canvasElement.height = targetHeight;

    const { sx, sy, sw, sh } = calculateCoverCrop(bitmap.width, bitmap.height, targetWidth, targetHeight);
    context.drawImage(bitmap, sx, sy, sw, sh, 0, 0, targetWidth, targetHeight);

    if (typeof bitmap.close === "function") {
        bitmap.close();
    }
}

function calculateCoverCrop(sourceWidth, sourceHeight, targetWidth, targetHeight) {
    const sourceAspect = sourceWidth / sourceHeight;
    const targetAspect = targetWidth / targetHeight;

    let sx = 0;
    let sy = 0;
    let sw = sourceWidth;
    let sh = sourceHeight;

    if (sourceAspect > targetAspect) {
        sw = sourceHeight * targetAspect;
        sx = (sourceWidth - sw) / 2;
    } else {
        sh = sourceWidth / targetAspect;
        sy = (sourceHeight - sh) / 2;
    }

    return { sx, sy, sw, sh };
}

async function canvasToBytes(canvasElement, captureOptions) {
    const mimeType = captureOptions?.mimeType ?? "image/jpeg";
    const quality = captureOptions?.quality ?? 0.7;

    const blob = await new Promise((resolve, reject) => {
        if (canvasElement.convertToBlob) {
            canvasElement.convertToBlob({ type: mimeType, quality }).then(resolve, reject);
        } else {
            canvasElement.toBlob((result) => {
                if (result) {
                    resolve(result);
                } else {
                    reject(new Error("Canvas konnte nicht in Blob umgewandelt werden."));
                }
            }, mimeType, quality);
        }
    });

    const buffer = await blob.arrayBuffer();
    return new Uint8Array(buffer);
}

async function tryCreateBitmap(file, captureOptions) {
    if (typeof createImageBitmap === "function") {
        try {
            return await createImageBitmap(file, {
                resizeWidth: captureOptions?.targetWidth,
                resizeHeight: captureOptions?.targetHeight,
                resizeQuality: "medium"
            });
        } catch (error) {
            console.warn("createImageBitmap fehlgeschlagen", error);
        }
    }

    return await new Promise((resolve, reject) => {
        const image = new Image();
        const objectUrl = URL.createObjectURL(file);
        image.onload = () => {
            URL.revokeObjectURL(objectUrl);
            resolve(image);
        };
        image.onerror = (event) => {
            URL.revokeObjectURL(objectUrl);
            reject(event?.error ?? new Error("Bild konnte nicht geladen werden."));
        };
        image.src = objectUrl;
    });
}

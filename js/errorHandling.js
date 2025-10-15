(function () {
    const errorUi = document.getElementById('blazor-error-ui');
    if (!errorUi) {
        return;
    }

    const detailsElement = errorUi.querySelector('[data-error-details]');
    const emailButton = errorUi.querySelector('[data-error-email]');
    const dismissElement = errorUi.querySelector('.dismiss');
    let lastDetailsText = '';
    let lastInteraction = null;

    function nowIso() {
        try {
            return new Date().toISOString();
        } catch (error) {
            return new Date().toString();
        }
    }

    function describeElement(element) {
        if (!element || !element.tagName) {
            return 'Unbekanntes Element';
        }

        const parts = [element.tagName.toLowerCase()];

        if (element.id) {
            parts.push('#' + element.id);
        }

        if (element.classList && element.classList.length > 0) {
            const classNames = Array.prototype.slice.call(element.classList, 0, 3);
            parts.push('.' + classNames.join('.'));
            if (element.classList.length > classNames.length) {
                parts.push('.…');
            }
        }

        if (element.getAttribute) {
            const name = element.getAttribute('name');
            if (name) {
                parts.push('[name="' + name + '"]');
            }

            const ariaLabel = element.getAttribute('aria-label');
            if (ariaLabel) {
                parts.push('[aria-label="' + ariaLabel + '"]');
            } else {
                const labelledBy = element.getAttribute('aria-labelledby');
                if (labelledBy) {
                    parts.push('[aria-labelledby="' + labelledBy + '"]');
                }
            }
        }

        return parts.join('');
    }

    function recordInteraction(event) {
        if (!event) {
            return;
        }

        const info = {
            type: event.type,
            time: nowIso(),
            target: describeElement(event.target)
        };

        if (event.type === 'keydown') {
            info.key = event.key;
        }

        if (event.type === 'pointerdown') {
            info.pointerType = event.pointerType || '';
            if (typeof event.clientX === 'number' && typeof event.clientY === 'number') {
                info.coordinates = event.clientX + ',' + event.clientY;
            }
        }

        if (event.type === 'touchstart' && event.touches && event.touches.length > 0) {
            const touch = event.touches[0];
            info.coordinates = touch.clientX + ',' + touch.clientY;
        }

        lastInteraction = info;
    }

    ['pointerdown', 'touchstart', 'click'].forEach(function (type) {
        window.addEventListener(type, recordInteraction, { capture: true, passive: true });
    });
    window.addEventListener('keydown', recordInteraction, true);

    function describeUnknownValue(value, seen) {
        if (!seen) {
            seen = [];
        }

        const isObject = typeof value === 'object' && value !== null;
        if (isObject) {
            if (seen.indexOf(value) !== -1) {
                return '[Circular]';
            }
            seen.push(value);
        }

        let result;

        if (value === null) {
            result = 'null';
        } else if (value === undefined) {
            result = 'undefined';
        } else if (typeof value === 'string') {
            result = value;
        } else if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
            result = String(value);
        } else if (typeof value === 'symbol') {
            result = value.toString();
        } else if (typeof value === 'function') {
            result = '[Function ' + (value.name || 'anonymous') + ']';
        } else if (value instanceof Error) {
            const name = value.name ? value.name + ': ' : '';
            result = name + (value.message || '');
        } else if (Array.isArray(value)) {
            const preview = value.slice(0, 5).map(function (item) {
                return describeUnknownValue(item, seen);
            });
            result = '[Array(' + value.length + ')] ' + preview.join(', ') + (value.length > 5 ? ', …' : '');
        } else if (value && value.nodeName) {
            result = '[Node ' + value.nodeName + ']';
        } else if (isObject) {
            const ctorName = value.constructor && value.constructor.name ? value.constructor.name : 'Object';
            const keys = Object.keys(value);
            if (keys.length === 0) {
                result = ctorName + ' {}';
            } else {
                const previewKeys = keys.slice(0, 8);
                const lines = previewKeys.map(function (key) {
                    let inner;
                    try {
                        inner = describeUnknownValue(value[key], seen);
                    } catch (error) {
                        inner = '[Unlesbar: ' + (error && error.message ? error.message : 'unbekannt') + ']';
                    }
                    return key + ': ' + inner;
                });
                if (keys.length > previewKeys.length) {
                    lines.push('…');
                }
                result = ctorName + ' {\n' + lines.join('\n') + '\n}';
            }
        } else {
            try {
                result = String(value);
            } catch (error) {
                result = Object.prototype.toString.call(value);
            }
        }

        if (isObject) {
            seen.pop();
        }

        return result;
    }

    function buildContextSections() {
        const sections = [];
        const interactionLines = [];

        if (lastInteraction) {
            interactionLines.push('Ereignis: ' + lastInteraction.type);
            interactionLines.push('Zeitpunkt: ' + lastInteraction.time);
            if (lastInteraction.target) {
                interactionLines.push('Ziel: ' + lastInteraction.target);
            }
            if (lastInteraction.key) {
                interactionLines.push('Taste: ' + lastInteraction.key);
            }
            if (lastInteraction.pointerType) {
                interactionLines.push('Pointer-Typ: ' + lastInteraction.pointerType);
            }
            if (lastInteraction.coordinates) {
                interactionLines.push('Koordinaten: ' + lastInteraction.coordinates);
            }
        } else {
            interactionLines.push('Keine Nutzeraktion seit dem Laden erfasst.');
        }

        sections.push({
            title: 'Letzte Nutzeraktion',
            content: interactionLines.join('\n')
        });

        const pageLines = [
            'URL: ' + window.location.href,
            'Sichtbarkeit: ' + (document.visibilityState || 'unbekannt'),
            'Online-Status: ' + (navigator.onLine ? 'online' : 'offline'),
            'Viewport: ' + window.innerWidth + 'x' + window.innerHeight,
            'Scrollposition: ' + window.scrollX + ',' + window.scrollY
        ];

        const connection = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        if (connection) {
            const connectionDetails = [];
            if (connection.effectiveType) {
                connectionDetails.push('Typ: ' + connection.effectiveType);
            }
            if (typeof connection.downlink === 'number') {
                connectionDetails.push('Downlink: ' + connection.downlink + 'Mb/s');
            }
            if (typeof connection.rtt === 'number') {
                connectionDetails.push('Latenz: ' + connection.rtt + 'ms');
            }
            if (connection.saveData) {
                connectionDetails.push('Datensparmodus: aktiv');
            }
            if (connectionDetails.length > 0) {
                pageLines.push('Netzwerk: ' + connectionDetails.join(', '));
            }
        }

        sections.push({
            title: 'Seitenzustand',
            content: pageLines.join('\n')
        });

        if ('serviceWorker' in navigator) {
            const controller = navigator.serviceWorker.controller;
            const serviceWorkerLines = [];

            if (controller) {
                serviceWorkerLines.push('Controller aktiv: ja');
                if (controller.scriptURL) {
                    serviceWorkerLines.push('Script: ' + controller.scriptURL);
                }
                if (controller.state) {
                    serviceWorkerLines.push('Status: ' + controller.state);
                }
            } else {
                serviceWorkerLines.push('Kein aktiver Service Worker Controller.');
            }

            sections.push({
                title: 'Service Worker',
                content: serviceWorkerLines.join('\n')
            });
        } else {
            sections.push({
                title: 'Service Worker',
                content: 'Nicht unterstützt.'
            });
        }

        return sections;
    }

    function formatDetails(details) {
        const lines = [];

        if (details.message) {
            lines.push(details.message);
        }

        if (details.stack && details.stack !== details.message) {
            lines.push(details.stack);
        }

        if (details.sections && details.sections.length > 0) {
            details.sections.forEach(function (section) {
                if (!section || !section.content) {
                    return;
                }

                lines.push('');
                if (section.title) {
                    lines.push(section.title + ':');
                }
                lines.push(section.content);
            });
        }

        if (lines.length === 0) {
            lines.push('Leider konnte keine detaillierte Fehlermeldung ermittelt werden.');
        }

        return lines.join('\n');
    }

    function ensureVisible() {
        if (getComputedStyle(errorUi).display === 'none') {
            errorUi.style.display = 'block';
        }
    }

    function getEmailConfiguration(attributeName, fallbackValue) {
        if (emailButton && emailButton.hasAttribute(attributeName)) {
            return emailButton.getAttribute(attributeName) || fallbackValue;
        }

        if (errorUi.hasAttribute(attributeName)) {
            return errorUi.getAttribute(attributeName) || fallbackValue;
        }

        return fallbackValue;
    }

    function updateEmail(detailsText) {
        if (!emailButton) {
            return;
        }

        const to = getEmailConfiguration('data-email-to', 'support@example.com');
        const subject = getEmailConfiguration('data-email-subject', 'Nasreddins Toolbox – Fehlerbericht');
        const intro = getEmailConfiguration('data-email-intro', 'Bitte beschreiben Sie kurz, was unmittelbar vor dem Fehler passiert ist:');

        const bodyLines = [
            intro,
            '',
            '--- Technische Details ---',
            detailsText,
            '',
            `User Agent: ${navigator.userAgent}`,
            `Zeitpunkt: ${nowIso()}`
        ];

        const body = encodeURIComponent(bodyLines.join('\n'));
        const mailto = `mailto:${encodeURIComponent(to)}?subject=${encodeURIComponent(subject)}&body=${body}`;

        emailButton.dataset.mailto = mailto;
        emailButton.disabled = false;
    }

    function showError(rawDetails) {
        const baseDetails = rawDetails || {};
        const combinedSections = (baseDetails.sections || []).concat(buildContextSections());
        const detailsText = formatDetails({
            message: baseDetails.message,
            stack: baseDetails.stack,
            sections: combinedSections
        });

        if (detailsText === lastDetailsText) {
            ensureVisible();
            return;
        }

        lastDetailsText = detailsText;

        if (detailsElement) {
            detailsElement.textContent = detailsText;
        }

        updateEmail(detailsText);
        ensureVisible();

        if (typeof console !== 'undefined' && typeof console.error === 'function') {
            const payload = {
                message: baseDetails.message || '',
                stack: baseDetails.stack || '',
                sections: combinedSections,
                timestamp: nowIso()
            };

            try {
                console.error('Nasreddins Toolbox – Fehlerdetails', payload);
            } catch (error) {
                try {
                    console.error('Nasreddins Toolbox – Fehlerdetails: ' + detailsText);
                } catch (ignore) {
                    // Ignoriert, wenn das Logging selbst fehlschlägt.
                }
            }
        }
    }

    function extractErrorEventSections(event) {
        const sections = [];
        if (!event) {
            return sections;
        }

        const locationLines = [];
        if (event.filename) {
            locationLines.push('Datei: ' + event.filename);
        }
        if (typeof event.lineno === 'number') {
            locationLines.push('Zeile: ' + event.lineno);
        }
        if (typeof event.colno === 'number') {
            locationLines.push('Spalte: ' + event.colno);
        }
        if (locationLines.length > 0) {
            sections.push({
                title: 'Quelle',
                content: locationLines.join('\n')
            });
        }

        if (event.error) {
            if (event.error.name) {
                sections.push({
                    title: 'Fehlertyp',
                    content: event.error.name
                });
            }
            if (event.error.cause !== undefined) {
                sections.push({
                    title: 'Ursache',
                    content: describeUnknownValue(event.error.cause)
                });
            }
        }

        return sections;
    }

    function extractReasonDetails(reason) {
        const sections = [];

        if (reason === null || reason === undefined) {
            return { message: '', stack: '', sections: sections };
        }

        if (typeof reason === 'string') {
            return { message: reason, stack: '', sections: sections };
        }

        if (reason instanceof Error) {
            const errorSections = [];
            if (reason.name) {
                errorSections.push({
                    title: 'Fehlertyp',
                    content: reason.name
                });
            }
            if (reason.cause !== undefined) {
                errorSections.push({
                    title: 'Ursache',
                    content: describeUnknownValue(reason.cause)
                });
            }
            return {
                message: reason.message || '',
                stack: reason.stack || '',
                sections: sections.concat(errorSections)
            };
        }

        let description;
        try {
            description = describeUnknownValue(reason);
        } catch (error) {
            description = 'Unbekannter Fehlergrund (Beschreibungsfehler: ' + (error && error.message ? error.message : 'unbekannt') + ')';
        }

        sections.push({
            title: 'Fehlerdaten',
            content: description
        });

        const message = reason && reason.message ? String(reason.message) : description;
        const stack = reason && reason.stack ? String(reason.stack) : '';

        return { message: message, stack: stack, sections: sections };
    }

    if (emailButton) {
        emailButton.disabled = true;
        emailButton.addEventListener('click', function (event) {
            event.preventDefault();
            const mailto = emailButton.dataset.mailto;
            if (mailto) {
                window.location.href = mailto;
            }
        });
    }

    window.addEventListener('error', (event) => {
        if (!event) {
            return;
        }

        let message = event.message || '';
        let stack = '';

        if (event.error) {
            if (!message && event.error.message) {
                message = event.error.message;
            }
            if (event.error.stack) {
                stack = event.error.stack;
            }
        }

        showError({
            message: message,
            stack: stack,
            sections: extractErrorEventSections(event)
        });
    });

    window.addEventListener('unhandledrejection', (event) => {
        if (!event) {
            return;
        }

        const reasonDetails = extractReasonDetails(event.reason);
        const sections = reasonDetails.sections.slice();

        if (event.promise) {
            sections.push({
                title: 'Promise',
                content: describeUnknownValue(event.promise)
            });
        }

        showError({
            message: reasonDetails.message,
            stack: reasonDetails.stack,
            sections: sections
        });
    });

    const observer = new MutationObserver((mutations) => {
        const isVisible = getComputedStyle(errorUi).display !== 'none';
        const hasDetails = detailsElement ? detailsElement.textContent.trim().length > 0 : false;

        if (isVisible && !hasDetails) {
            const mutationLines = mutations.map((mutation) => {
                if (mutation.type === 'attributes') {
                    const attributeName = mutation.attributeName;
                    const newValue = attributeName ? errorUi.getAttribute(attributeName) : null;
                    return 'Attribut geändert: ' + attributeName + ' → ' + (newValue !== null ? newValue : '(entfernt)');
                }
                return 'Mutationstyp: ' + mutation.type;
            });

            if (typeof console !== 'undefined' && typeof console.warn === 'function') {
                try {
                    console.warn('Fehleranzeige ohne Details sichtbar geworden.', {
                        mutations: mutationLines,
                        timestamp: nowIso()
                    });
                } catch (error) {
                    console.warn('Fehleranzeige ohne Details sichtbar geworden.');
                }
            }

            showError({
                message: 'Es ist ein unbekannter Fehler aufgetreten.',
                stack: '',
                sections: mutationLines.length > 0 ? [{
                    title: 'Mutation Observer',
                    content: mutationLines.join('\n')
                }] : []
            });
        }
    });

    observer.observe(errorUi, { attributes: true, attributeFilter: ['style', 'class'] });

    if (dismissElement) {
        dismissElement.addEventListener('click', () => observer.disconnect());
    }
})();

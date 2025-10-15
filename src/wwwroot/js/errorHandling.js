(function () {
    const errorUi = document.getElementById('blazor-error-ui');
    if (!errorUi) {
        return;
    }

    const detailsElement = errorUi.querySelector('[data-error-details]');
    const emailButton = errorUi.querySelector('[data-error-email]');
    const dismissElement = errorUi.querySelector('.dismiss');
    let lastDetailsText = '';

    function ensureVisible() {
        if (getComputedStyle(errorUi).display === 'none') {
            errorUi.style.display = 'block';
        }
    }

    function formatDetails(message, stack) {
        const parts = [];
        if (message) {
            parts.push(message);
        }
        if (stack && stack !== message) {
            parts.push(stack);
        }

        if (parts.length === 0) {
            parts.push('Leider konnte keine detaillierte Fehlermeldung ermittelt werden.');
        }

        return parts.join('\n\n');
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
            `Zeitpunkt: ${new Date().toISOString()}`
        ];

        const body = encodeURIComponent(bodyLines.join('\n'));
        const mailto = `mailto:${encodeURIComponent(to)}?subject=${encodeURIComponent(subject)}&body=${body}`;

        emailButton.dataset.mailto = mailto;
        emailButton.disabled = false;
    }

    function showError(message, stack) {
        const detailsText = formatDetails(message, stack);

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
    }

    if (emailButton) {
        emailButton.disabled = true;
        emailButton.addEventListener('click', (event) => {
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

        const message = event.message || '';
        const stack = event.error && event.error.stack ? event.error.stack : '';
        showError(message, stack);
    });

    window.addEventListener('unhandledrejection', (event) => {
        if (!event) {
            return;
        }

        const reason = event.reason;
        let message = '';
        let stack = '';

        if (typeof reason === 'string') {
            message = reason;
        } else if (reason) {
            message = reason.message || '';
            stack = reason.stack || '';
        }

        showError(message, stack);
    });

    const observer = new MutationObserver(() => {
        const isVisible = getComputedStyle(errorUi).display !== 'none';
        const hasDetails = detailsElement ? detailsElement.textContent.trim().length > 0 : false;

        if (isVisible && !hasDetails) {
            showError('Es ist ein unbekannter Fehler aufgetreten.', '');
        }
    });

    observer.observe(errorUi, { attributes: true, attributeFilter: ['style', 'class'] });

    if (dismissElement) {
        dismissElement.addEventListener('click', () => observer.disconnect());
    }
})();

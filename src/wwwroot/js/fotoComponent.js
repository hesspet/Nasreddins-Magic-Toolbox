(function () {
    const namespace = window.fotoComponent = window.fotoComponent || {};

    namespace.triggerCapture = function (input) {
        if (!input) {
            return;
        }

        try {
            input.value = "";
            input.dispatchEvent(new Event("input", { bubbles: true }));
            input.click();
        } catch (err) {
            console.error("Failed to trigger camera capture", err);
        }
    };
})();

window.theme = window.theme || {};

window.theme.applyTheme = function (themeName) {
    var normalized = (themeName || "").toString().trim().toLowerCase();

    if (normalized !== "dark" && normalized !== "light") {
        normalized = "light";
    }

    document.documentElement.dataset.theme = normalized;
};

window.theme.getTheme = function () {
    return document.documentElement.dataset.theme || "light";
};

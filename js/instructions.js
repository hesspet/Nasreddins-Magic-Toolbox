export function scrollToHeading(container, headingId) {
    if (!container || !headingId) {
        return;
    }

    const escapeId = typeof CSS !== "undefined" && typeof CSS.escape === "function"
        ? CSS.escape(headingId)
        : headingId.replace(/([\0-\x1F\x7F-\x9F\s#.;?%&,+*~':"^$\[\]()=>|/@])/g, '\\$1');

    const target = container.querySelector(`#${escapeId}`);

    if (target) {
        target.scrollIntoView({ behavior: "smooth", block: "start", inline: "nearest" });
    }
}

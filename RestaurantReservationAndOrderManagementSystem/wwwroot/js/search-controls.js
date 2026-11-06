document.addEventListener("click", (event) => {
    if (!(event.target instanceof Element)) {
        return;
    }

    const clearButton = event.target.closest(".system-search-clear");
    if (!clearButton) {
        return;
    }

    const search = clearButton.closest(".system-search");
    const input = search?.querySelector('input[type="search"]');
    if (!input) {
        return;
    }

    input.value = "";
    input.dispatchEvent(new Event("input", { bubbles: true }));
});

document.addEventListener("submit", (event) => {
    if (event.target instanceof HTMLFormElement && event.target.hasAttribute("data-live-search")) {
        event.preventDefault();
    }
});

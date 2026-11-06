(function () {
    const trigger = document.getElementById("openAddTableModal");
    const modal = document.getElementById("addTableModal");
    if (!trigger || !modal) return;

    trigger.addEventListener("click", () => {
        if (typeof modal.showModal === "function") {
            modal.showModal();
        } else {
            modal.setAttribute("open", "");
        }
    });

    modal.querySelectorAll("[data-close-table-modal]").forEach(button => {
        button.addEventListener("click", () => modal.close());
    });

    modal.addEventListener("click", event => {
        if (event.target === modal && typeof modal.close === "function") {
            modal.close();
        }
    });
})();

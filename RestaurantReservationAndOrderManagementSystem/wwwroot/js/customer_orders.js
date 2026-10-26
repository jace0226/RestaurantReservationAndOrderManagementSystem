document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".customer-order-summary-toggle").forEach((button) => {
        button.addEventListener("click", () => {
            const summaryId = button.getAttribute("aria-controls");
            const summary = summaryId ? document.getElementById(summaryId) : null;
            if (!summary) {
                return;
            }

            const isExpanded = button.getAttribute("aria-expanded") === "true";
            button.setAttribute("aria-expanded", String(!isExpanded));
            summary.hidden = isExpanded;
        });
    });

    document.querySelectorAll("[data-confirm-cancel]").forEach((form) => {
        form.addEventListener("submit", (event) => {
            if (!window.confirm("Cancel this order?")) {
                event.preventDefault();
            }
        });
    });

    document.querySelectorAll("[data-unavailable-feature]").forEach((button) => {
        button.addEventListener("click", () => {
            const feature = button.getAttribute("data-unavailable-feature");
            const message = feature === "Invoice"
                ? "Invoice viewing is not available yet."
                : "Online payment is not available yet.";
            window.alert(message);
        });
    });
});

// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

const customerSidebar = document.querySelector(".customer-sidebar");
const customerSidebarToggle = customerSidebar?.querySelector(".customer-sidebar-toggle");

if (customerSidebar && customerSidebarToggle) {
    customerSidebarToggle.addEventListener("click", () => {
        const isOpen = customerSidebar.classList.toggle("is-open");
        customerSidebarToggle.setAttribute("aria-expanded", String(isOpen));
        customerSidebarToggle.setAttribute("aria-label", isOpen ? "Close navigation menu" : "Open navigation menu");
    });

    customerSidebar.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && customerSidebar.classList.contains("is-open")) {
            customerSidebar.classList.remove("is-open");
            customerSidebarToggle.setAttribute("aria-expanded", "false");
            customerSidebarToggle.setAttribute("aria-label", "Open navigation menu");
            customerSidebarToggle.focus();
        }
    });
}

(() => {
    // Read items initialized from Razor Page model
    const menuItems = window.menuItemsData || [];

    const menuGrid = document.getElementById("customerMenuGrid");
    const recommendationGrid = document.getElementById("recommendationGrid");
    const categoryFilters = document.getElementById("customerCategoryFilters");
    const searchInput = document.getElementById("customerMenuSearch");
    const resultCount = document.getElementById("customerMenuResultCount");
    const emptyState = document.getElementById("customerMenuEmpty");
    const modal = document.getElementById("customerFoodModal");
    const modalPanel = modal.querySelector(".customer-modal-panel");
    let selectedCategory = "All";
    let lastFocusedElement = null;

    const formatPrice = (price) => {
        const numPrice = typeof price === 'number' ? price : Number(price) || 0;
        return `RM${numPrice.toFixed(2)}`;
    };

    function makeElement(tagName, className, text) {
        const element = document.createElement(tagName);
        if (className) {
            element.className = className;
        }
        if (text !== undefined) {
            element.textContent = text;
        }
        return element;
    }

    function makeImage(item, className) {
        const image = document.createElement("img");
        image.className = className;
        image.src = item.image;
        image.alt = item.imageAlt || item.name;
        image.loading = "lazy";
        return image;
    }

    function createMenuCard(item) {
        const card = makeElement("article", "customer-food-card");
        card.id = `dish-${item.id}`;

        const imageWrap = makeElement("div", "customer-food-image-wrap");
        const categoryTag = makeElement("span", "customer-category-tag", item.category);
        imageWrap.append(makeImage(item, "customer-food-image"), categoryTag);

        const content = makeElement("div", "customer-food-content");
        const titleRow = makeElement("div", "customer-food-title-row");
        titleRow.append(
            makeElement("h3", "", item.name),
            makeElement("span", "customer-food-price", formatPrice(item.price))
        );

        const footer = makeElement("div", "customer-food-footer");
        const availability = makeElement(
            "span",
            `customer-food-availability${item.available ? "" : " unavailable"}`,
            item.available ? "Available today" : "Currently unavailable"
        );
        const detailsButton = makeElement("button", "customer-view-details", "View Details");
        detailsButton.type = "button";
        detailsButton.addEventListener("click", () => openDetails(item, detailsButton));
        footer.append(availability, detailsButton);

        content.append(
            titleRow,
            makeElement("p", "customer-food-description", item.description),
            footer
        );
        card.append(imageWrap, content);
        return card;
    }

    function scrollToDish(itemId) {
        if (selectedCategory !== "All" || searchInput.value.trim() !== "") {
            resetFilters();
        }

        setTimeout(() => {
            const targetElement = document.getElementById(`dish-${itemId}`);
            if (targetElement) {
                targetElement.scrollIntoView({ behavior: "smooth", block: "start" });
                targetElement.classList.add("highlight-target");
                setTimeout(() => {
                    targetElement.classList.remove("highlight-target");
                }, 1500);
            }
        }, 50);
    }

    function createRecommendationCard(item) {
        const card = makeElement("article", "customer-recommendation-card");
        card.tabIndex = 0;
        card.setAttribute("role", "button");
        card.setAttribute("aria-label", `Jump to ${item.name}`);

        const details = makeElement("div", "customer-recommendation-details");
        details.append(
            makeElement("span", "customer-recommendation-label", item.recommendation),
            makeElement("h3", "", item.name),
            makeElement("span", "customer-recommendation-price", formatPrice(item.price))
        );
        card.append(makeImage(item, "customer-recommendation-image"), details);

        card.addEventListener("click", () => scrollToDish(item.id));
        card.addEventListener("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                scrollToDish(item.id);
            }
        });

        return card;
    }

    function renderRecommendations() {
        const recommendations = menuItems.filter((item) => item.recommendation && item.recommendation !== "").slice(0, 3);
        recommendationGrid.replaceChildren(...recommendations.map(createRecommendationCard));
    }

    function renderMenu() {
        const query = searchInput.value.trim().toLocaleLowerCase();
        const filteredItems = menuItems.filter((item) => {
            const matchesCategory = selectedCategory === "All" || item.category === selectedCategory;
            const matchesSearch = item.name.toLocaleLowerCase().includes(query);
            return matchesCategory && matchesSearch;
        });

        menuGrid.replaceChildren(...filteredItems.map(createMenuCard));
        resultCount.textContent = `${filteredItems.length} ${filteredItems.length === 1 ? "dish" : "dishes"}`;
        emptyState.hidden = filteredItems.length > 0;
        menuGrid.hidden = filteredItems.length === 0;
    }

    function openDetails(item, trigger) {
        lastFocusedElement = trigger;
        document.getElementById("customerModalImage").src = item.image;
        document.getElementById("customerModalImage").alt = item.imageAlt;
        document.getElementById("customerModalCategory").textContent = item.category;
        document.getElementById("customerModalTitle").textContent = item.name;
        document.getElementById("customerModalPrice").textContent = formatPrice(item.price);
        document.getElementById("customerModalDescription").textContent = item.fullDescription;
        document.getElementById("customerModalPrepTime").textContent = item.prepTime;
        document.getElementById("customerModalAvailability").textContent = item.available
            ? "Available today"
            : "Currently unavailable";
        document.getElementById("customerModalDietary").textContent = item.dietary;

        const addOrderBtn = document.getElementById("customerAddOrderBtn");
        if (addOrderBtn) {
            addOrderBtn.hidden = !item.available;
            addOrderBtn.onclick = () => {
                alert(`Added ${item.name} to your order!`);
                closeDetails();
            };
        }

        modal.hidden = false;
        document.body.classList.add("customer-modal-open");
        modalPanel.focus();
    }

    function closeDetails() {
        if (modal.hidden) return;
        modal.hidden = true;
        document.body.classList.remove("customer-modal-open");
        if (lastFocusedElement?.isConnected) {
            lastFocusedElement.focus();
        }
    }

    function resetFilters() {
        searchInput.value = "";
        selectedCategory = "All";
        categoryFilters.querySelectorAll("button").forEach((button) => {
            const isSelected = button.dataset.category === selectedCategory;
            button.classList.toggle("active", isSelected);
            button.setAttribute("aria-pressed", String(isSelected));
        });
        renderMenu();
        searchInput.focus();
    }

    categoryFilters.addEventListener("click", (event) => {
        if (!(event.target instanceof Element)) return;
        const button = event.target.closest("button[data-category]");
        if (!button) return;

        selectedCategory = button.dataset.category;
        categoryFilters.querySelectorAll("button").forEach((filterButton) => {
            const isSelected = filterButton === button;
            filterButton.classList.toggle("active", isSelected);
            filterButton.setAttribute("aria-pressed", String(isSelected));
        });
        renderMenu();
    });

    searchInput.addEventListener("input", renderMenu);
    document.getElementById("customerMenuReset").addEventListener("click", resetFilters);
    document.getElementById("customerEmptyReset").addEventListener("click", resetFilters);
    modal.querySelectorAll("[data-close-modal]").forEach((button) => {
        button.addEventListener("click", closeDetails);
    });
    modal.addEventListener("click", (event) => {
        if (event.target === modal) closeDetails();
    });
    document.addEventListener("keydown", (event) => {
        if (modal.hidden) return;
        if (event.key === "Escape") closeDetails();
        else if (event.key === "Tab") {
            event.preventDefault();
            modal.querySelector("[data-close-modal]").focus();
        }
    });

    renderRecommendations();
    renderMenu();
})();


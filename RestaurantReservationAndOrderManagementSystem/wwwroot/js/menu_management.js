(() => {
    const items = window.staffMenuItemsData || [];
    const modal = document.getElementById("menuItemModal");
    const panel = modal?.querySelector(".staff-menu-form-panel");
    const form = document.getElementById("menuItemForm");
    const openAddButton = document.getElementById("open-add-menu");
    const title = document.getElementById("menuFormTitle");
    const saveButton = document.getElementById("saveMenuButton");
    const deleteButton = document.getElementById("deleteMenuButton");
    const deleteForm = document.getElementById("deleteMenuForm");
    const deleteFoodId = document.getElementById("deleteFoodId");
    const imageInput = document.getElementById("Input_Image");
    const imagePreview = document.getElementById("menuImagePreview");
    const imagePlaceholder = document.getElementById("menuImagePlaceholder");
    const searchInput = document.getElementById("staffMenuSearch");
    const categoryFilters = document.getElementById("staffCategoryFilters");
    const availabilityFilters = document.getElementById("staffAvailabilityFilters");
    const resultCount = document.getElementById("staffMenuResultCount");
    const emptyState = document.getElementById("staffMenuEmpty");
    const menuGrid = document.getElementById("staffMenuGrid");

    let selectedCategory = "All";
    let selectedAvailability = window.staffMenuAvailabilityFilter || "All";
    let previousFocus = null;
    let previewObjectUrl = null;

    function setPreview(source, alt, isObjectUrl = false) {
        if (previewObjectUrl) {
            URL.revokeObjectURL(previewObjectUrl);
            previewObjectUrl = null;
        }

        if (source) {
            previewObjectUrl = isObjectUrl ? source : null;
            imagePreview.src = source;
            imagePreview.alt = alt || "Menu item image preview";
            imagePreview.hidden = false;
            imagePlaceholder.hidden = true;
        } else {
            imagePreview.removeAttribute("src");
            imagePreview.alt = "";
            imagePreview.hidden = true;
            imagePlaceholder.hidden = false;
        }
    }

    function openModal() {
        previousFocus = document.activeElement;
        modal.hidden = false;
        document.body.classList.add("staff-menu-modal-open");
        panel.focus();
    }

    function closeModal() {
        modal.hidden = true;
        document.body.classList.remove("staff-menu-modal-open");
        if (previousFocus instanceof HTMLElement) {
            previousFocus.focus();
        }
    }

    function clearValidationMessages() {
        form.querySelectorAll(".staff-field-error").forEach((element) => {
            element.textContent = "";
        });
        form.querySelectorAll(".staff-validation-summary").forEach((element) => {
            element.replaceChildren();
        });
    }

    function startNewItem() {
        document.getElementById("Input_FoodId").value = "";
        document.getElementById("Input_Name").value = "";
        document.getElementById("Input_Price").value = "";
        document.getElementById("Input_Description").value = "";
        document.getElementById("Input_FullDescription").value = "";
        document.getElementById("Input_Category").selectedIndex = 0;
        document.getElementById("Input_Dietary").value = "Standard";
        document.getElementById("Input_PrepTime").value = "15";
        document.getElementById("Input_Availability").value = "Available";
        imageInput.value = "";
        deleteFoodId.value = "";
        deleteButton.hidden = true;
        title.textContent = "Add Menu Item";
        saveButton.textContent = "Add Menu";
        setPreview("", "");
        clearValidationMessages();
        openModal();
    }

    function startEditItem(itemId) {
        const item = items.find((candidate) => candidate.id === itemId);
        if (!item) {
            return;
        }

        document.getElementById("Input_FoodId").value = item.id;
        document.getElementById("Input_Name").value = item.name;
        document.getElementById("Input_Price").value = Number(item.price).toFixed(2);
        document.getElementById("Input_Description").value = item.description || "";
        document.getElementById("Input_FullDescription").value = item.fullDescription || "";
        document.getElementById("Input_Category").value = item.category || "";
        document.getElementById("Input_Dietary").value = item.dietary || "Standard";
        document.getElementById("Input_PrepTime").value =
            (item.prepTime || "").replace(/\s*(mins?|minutes?)\s*$/i, "");
        document.getElementById("Input_Availability").value =
            /^available$/i.test(item.availability) ? "Available" : "Unavailable";
        imageInput.value = "";
        deleteFoodId.value = item.id;
        deleteButton.hidden = false;
        title.textContent = "Edit Menu Item";
        saveButton.textContent = "Save Changes";
        setPreview(item.image, item.imageAlt || item.name);
        clearValidationMessages();
        openModal();
    }

    function updateMenu() {
        const query = searchInput.value.trim().toLocaleLowerCase();
        let visibleCount = 0;

        menuGrid.querySelectorAll(".staff-food-card").forEach((card) => {
            const matchesCategory = selectedCategory === "All" ||
                card.dataset.category === selectedCategory;
            const matchesAvailability = selectedAvailability === "All" ||
                card.dataset.availability === selectedAvailability;
            const matchesSearch = card.textContent.toLocaleLowerCase().includes(query);
            const isVisible = matchesCategory && matchesAvailability && matchesSearch;
            card.hidden = !isVisible;
            if (isVisible) {
                visibleCount += 1;
            }
        });

        resultCount.textContent = `${visibleCount} ${visibleCount === 1 ? "dish" : "dishes"}`;
        emptyState.hidden = visibleCount > 0;
        menuGrid.hidden = visibleCount === 0;
        document.querySelectorAll("input[name='search']").forEach((input) => {
            input.value = searchInput.value;
        });
        document.querySelectorAll("input[name='availabilityFilter']").forEach((input) => {
            input.value = selectedAvailability;
        });
    }

    if (modal) {
        openAddButton.addEventListener("click", startNewItem);
        document.querySelectorAll("[data-close-menu-modal]").forEach((button) => {
            button.addEventListener("click", closeModal);
        });

        document.querySelectorAll("[data-edit-food]").forEach((button) => {
            button.addEventListener("click", () => startEditItem(button.dataset.editFood));
        });

        imageInput.addEventListener("change", () => {
            const image = imageInput.files && imageInput.files[0];
            if (!image) {
                return;
            }

            setPreview(URL.createObjectURL(image), image.name, true);
        });

        deleteForm.addEventListener("submit", (event) => {
            if (!window.confirm("Delete this menu item? This action cannot be undone.")) {
                event.preventDefault();
            }
        });

        if (window.staffMenuFormOpen) {
            openModal();
        }
    }

    categoryFilters.querySelectorAll("button").forEach((button) => {
        button.addEventListener("click", () => {
            selectedCategory = button.dataset.category;
            categoryFilters.querySelectorAll("button").forEach((filter) => {
                const active = filter === button;
                filter.classList.toggle("active", active);
                filter.setAttribute("aria-pressed", String(active));
            });
            updateMenu();
        });
    });

    availabilityFilters.querySelectorAll("a").forEach((filter) => {
        filter.addEventListener("click", (event) => {
            event.preventDefault();
            selectedAvailability = new URL(filter.href).searchParams.get("availabilityFilter") || "All";
            availabilityFilters.querySelectorAll("a").forEach((candidate) => {
                const active = candidate === filter;
                candidate.classList.toggle("active", active);
                candidate.setAttribute("aria-pressed", String(active));
            });
            updateMenu();
        });
    });

    searchInput.addEventListener("input", updateMenu);
    document.getElementById("staffMenuReset").addEventListener("click", () => {
        searchInput.value = "";
        selectedCategory = "All";
        selectedAvailability = "All";
        categoryFilters.querySelectorAll("button").forEach((button) => {
            const active = button.dataset.category === "All";
            button.classList.toggle("active", active);
            button.setAttribute("aria-pressed", String(active));
        });
        availabilityFilters.querySelectorAll("a").forEach((filter) => {
            const active = new URL(filter.href).searchParams.get("availabilityFilter") === "All";
            filter.classList.toggle("active", active);
            filter.setAttribute("aria-pressed", String(active));
        });
        updateMenu();
        searchInput.focus();
    });

    document.getElementById("staffEmptyReset").addEventListener("click", () => {
        document.getElementById("staffMenuReset").click();
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape" && modal && !modal.hidden) {
            closeModal();
        }
    });

    updateMenu();
})();

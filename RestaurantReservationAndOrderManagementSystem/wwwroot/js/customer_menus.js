(() => {
    const menuItems = window.menuItemsData || [];
    const availableTables = window.availableTablesData || [];
    const activeBooking = window.activeBookingData || null;

    const menuGrid = document.getElementById("customerMenuGrid");
    const recommendationGrid = document.getElementById("recommendationGrid");
    const categoryFilters = document.getElementById("customerCategoryFilters");
    const searchInput = document.getElementById("customerMenuSearch");
    const resultCount = document.getElementById("customerMenuResultCount");
    const emptyState = document.getElementById("customerMenuEmpty");
    const modal = document.getElementById("customerFoodModal");
    const modalPanel = modal.querySelector(".customer-modal-panel");
    const cartButton = document.getElementById("customerCartToggle");
    const cartPanel = document.getElementById("customerCartPanel");
    const cartOverlay = document.getElementById("customerCartOverlay");
    const cartItemsContainer = document.getElementById("customerCartItems");
    const cartEmptyState = document.getElementById("customerCartEmpty");
    const cartBadge = document.getElementById("customerCartBadge");
    const cartSubtotal = document.getElementById("customerCartSubtotal");
    const cartTax = document.getElementById("customerCartTax");
    const cartTotal = document.getElementById("customerCartTotal");
    const confirmOrderButton = document.getElementById("customerConfirmOrder");
    const tableSelect = document.getElementById("customerTableSelect");
    const quantityValue = document.getElementById("customerQtyValue");
    const quantityPlus = document.getElementById("customerQtyPlus");
    const quantityMinus = document.getElementById("customerQtyMinus");
    const orderRemarks = document.getElementById("customerOrderRemarks");
    const orderCustomizations = document.getElementById("customerOrderCustomizations");
    const addOrderButton = document.getElementById("customerAddOrderBtn");
    const cartCloseButton = document.getElementById("customerCartClose");

    let selectedCategory = "All";
    let lastFocusedElement = null;
    let currentDraftItem = null;
    let currentQuantity = 1;
    let cartButtonWasDragged = false;
    let cartButtonDragState = null;
    const cart = [];

    const formatPrice = (price) => {
        const numPrice = typeof price === "number" ? price : Number(price) || 0;
        return `RM${numPrice.toFixed(2)}`;
    };

    function isItemAvailable(item) {
        if (typeof item.foodAvailability === "boolean") return item.foodAvailability;
        const status = (item.foodAvailability || "").toLowerCase();
        return status === "available" || status === "true";
    }

    function populateTableOptions() {
        if (!tableSelect) return;
        tableSelect.innerHTML = '<option value="" selected disabled>-- Choose Table --</option>';

        // SCENARIO A: ADVANCE BOOKING FOUND
        if (activeBooking && activeBooking.tableId) {
            const opt = document.createElement("option");
            opt.value = activeBooking.tableId;
            opt.textContent = `${activeBooking.tableId} (Reserved)`;
            opt.selected = true;
            tableSelect.appendChild(opt);

            // Lock dropdown since staff assigned this table
            tableSelect.disabled = true;
            validateOrderButton();
            return;
        }

        // SCENARIO B: WALK-IN (FALLBACK DROPDOWN)
        tableSelect.disabled = false;
        if (availableTables.length === 0) {
            const noOpt = document.createElement("option");
            noOpt.value = "";
            noOpt.textContent = "No dine-in tables available";
            noOpt.disabled = true;
            tableSelect.appendChild(noOpt);
            return;
        }

        availableTables.forEach((t) => {
            const opt = document.createElement("option");
            const id = t.tableID || t.tableId;
            opt.value = id;
            opt.textContent = id;
            tableSelect.appendChild(opt);
        });
    }

    function validateOrderButton() {
        const isTableSelected = tableSelect && tableSelect.value !== "";
        const hasItems = cart.length > 0;
        confirmOrderButton.disabled = !hasItems || !isTableSelected;
    }

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
        image.src = item.imageUrl || "/images/placeholder-food.jpg";
        image.alt = item.imageAlt || item.foodName || "Food Image";
        image.loading = "lazy";
        return image;
    }

    function isBeverage(item) {
        return (item.category || "").toLowerCase().includes("beverage");
    }

    function createMenuCard(item) {
        const card = makeElement("article", "customer-food-card");
        const itemId = item.foodID || item.foodId;
        card.id = `dish-${itemId}`;

        const available = isItemAvailable(item);

        const imageWrap = makeElement("div", "customer-food-image-wrap");
        const categoryTag = makeElement("span", "customer-category-tag", item.category || "Main Course");
        imageWrap.append(makeImage(item, "customer-food-image"), categoryTag);

        const content = makeElement("div", "customer-food-content");
        const titleRow = makeElement("div", "customer-food-title-row");
        titleRow.append(
            makeElement("h3", "", item.foodName || "Unnamed Dish"),
            makeElement("span", "customer-food-price", formatPrice(item.price))
        );

        const footer = makeElement("div", "customer-food-footer");
        const availability = makeElement(
            "span",
            `customer-food-availability${available ? "" : " unavailable"}`,
            available ? "Available today" : "Currently unavailable"
        );
        const addOrderButtonCard = makeElement("button", "customer-view-details", "Add Order");
        addOrderButtonCard.type = "button";
        addOrderButtonCard.disabled = !available;
        addOrderButtonCard.setAttribute("aria-label", `Add ${item.foodName} to the order`);
        addOrderButtonCard.addEventListener("click", () => openOrderModal(item, addOrderButtonCard));
        footer.append(availability, addOrderButtonCard);

        content.append(
            titleRow,
            makeElement("p", "customer-food-description", item.foodDescription || ""),
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
        const itemId = item.foodID || item.foodId;
        card.tabIndex = 0;
        card.setAttribute("role", "button");
        card.setAttribute("aria-label", `Jump to ${item.foodName}`);

        const details = makeElement("div", "customer-recommendation-details");
        details.append(
            makeElement("span", "customer-recommendation-label", item.recommendation),
            makeElement("h3", "", item.foodName),
            makeElement("span", "customer-recommendation-price", formatPrice(item.price))
        );
        card.append(makeImage(item, "customer-recommendation-image"), details);

        card.addEventListener("click", () => scrollToDish(itemId));
        card.addEventListener("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                scrollToDish(itemId);
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
            const matchesSearch = (item.foodName || "").toLocaleLowerCase().includes(query);
            return matchesCategory && matchesSearch;
        });

        menuGrid.replaceChildren(...filteredItems.map(createMenuCard));
        resultCount.textContent = `${filteredItems.length} ${filteredItems.length === 1 ? "dish" : "dishes"}`;
        emptyState.hidden = filteredItems.length > 0;
        menuGrid.hidden = filteredItems.length === 0;
    }

    function populateCustomizationOptions(item) {
        orderCustomizations.innerHTML = "";

        if (!isBeverage(item)) {
            return;
        }

        const groups = [
            { title: "Sugar level", key: "sugar", values: ["Less Sugar", "No Sugar"] },
            { title: "Ice level", key: "ice", values: ["Less Ice", "No Ice"] }
        ];

        groups.forEach((group) => {
            const wrapper = makeElement("div", "customer-customization-group");
            const label = makeElement("span", "customer-customization-label", group.title);
            wrapper.appendChild(label);

            group.values.forEach((value) => {
                const option = makeElement("label", "customer-option-row");
                const checkbox = document.createElement("input");
                checkbox.type = "checkbox";
                checkbox.name = `${group.key}-option`;
                checkbox.value = value;
                checkbox.dataset.optionGroup = group.key;
                checkbox.addEventListener("change", (event) => {
                    const target = event.currentTarget;
                    const sameGroup = orderCustomizations.querySelectorAll(`input[data-option-group="${group.key}"]`);
                    sameGroup.forEach((checkboxEl) => {
                        if (checkboxEl !== target) {
                            checkboxEl.checked = false;
                        }
                    });

                    if (target.checked) {
                        currentDraftItem[group.key] = target.value;
                    } else {
                        currentDraftItem[group.key] = "";
                    }
                });
                option.append(checkbox, document.createTextNode(value));
                wrapper.appendChild(option);
            });

            orderCustomizations.appendChild(wrapper);
        });
    }

    function updateQuantityUI() {
        quantityValue.textContent = String(currentQuantity);
        quantityMinus.innerHTML = currentQuantity > 1 ? "-" : '<i class="fa-solid fa-trash" aria-hidden="true"></i>';
        quantityMinus.classList.toggle("customer-quantity-remove", currentQuantity <= 1);
    }

    function openOrderModal(item, trigger) {
        lastFocusedElement = trigger;
        currentDraftItem = {
            item,
            sugar: "",
            ice: "",
            remarks: "",
            quantity: 1
        };
        currentQuantity = 1;

        const available = isItemAvailable(item);

        document.getElementById("customerModalImage").src = item.imageUrl || "/images/placeholder-food.jpg";
        document.getElementById("customerModalImage").alt = item.imageAlt || item.foodName;
        document.getElementById("customerModalCategory").textContent = item.category || "Main Course";
        document.getElementById("customerModalTitle").textContent = item.foodName;
        document.getElementById("customerModalPrice").textContent = formatPrice(item.price);
        document.getElementById("customerModalDescription").textContent = item.fullDescription || item.foodDescription || "";
        document.getElementById("customerModalPrepTime").textContent = item.prepTime || "15-20 mins";
        document.getElementById("customerModalAvailability").textContent = available ? "Available today" : "Currently unavailable";
        document.getElementById("customerModalDietary").textContent = item.dietary || "Standard";

        populateCustomizationOptions(item);
        orderRemarks.value = "";
        updateQuantityUI();

        addOrderButton.hidden = !available;
        addOrderButton.innerHTML = '<i class="fa-solid fa-plus" aria-hidden="true"></i> Add to Order';
        addOrderButton.onclick = () => {
            if (!available) return;
            addItemToCart();
            closeDetails();
        };

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

    function getCartEntryKey(cartItem) {
        return `${cartItem.foodId}|${cartItem.name}|${cartItem.sugar || ""}|${cartItem.ice || ""}|${(cartItem.remarks || "").trim()}`;
    }

    function addItemToCart() {
        if (!currentDraftItem || !currentDraftItem.item) return;

        const item = currentDraftItem.item;
        const newSelection = {
            foodId: item.foodID || item.foodId,
            name: item.foodName,
            image: item.imageUrl || "/images/placeholder-food.jpg",
            price: Number(item.price) || 0,
            quantity: currentQuantity,
            sugar: currentDraftItem.sugar || "",
            ice: currentDraftItem.ice || "",
            remarks: (currentDraftItem.remarks || "").trim()
        };

        const existing = cart.find((cartItem) => getCartEntryKey(cartItem) === getCartEntryKey(newSelection));
        if (existing) {
            existing.quantity += currentQuantity;
        } else {
            cart.push(newSelection);
        }

        updateCartSummary();
    }

    function buildSelectionText(entry) {
        const selected = [];
        if (entry.sugar) selected.push(entry.sugar);
        if (entry.ice) selected.push(entry.ice);
        return selected.join(", ");
    }

    function updateCartSummary() {
        const subtotal = cart.reduce((total, item) => total + Number(item.price || 0) * Number(item.quantity || 0), 0);
        const tax = subtotal * 0.1;
        const total = subtotal + tax;

        cartSubtotal.textContent = formatPrice(subtotal);
        cartTax.textContent = formatPrice(tax);
        cartTotal.textContent = formatPrice(total);

        const totalUnits = cart.reduce((total, item) => total + Number(item.quantity || 0), 0);
        cartBadge.textContent = String(totalUnits);
        cartBadge.hidden = totalUnits === 0;

        validateOrderButton();

        if (cart.length === 0) {
            cartEmptyState.hidden = false;
            cartItemsContainer.innerHTML = "";
            cartItemsContainer.classList.add("empty");
            return;
        }

        cartEmptyState.hidden = true;
        cartItemsContainer.classList.remove("empty");
        cartItemsContainer.innerHTML = "";

        cart.forEach((entry, index) => {
            const row = makeElement("div", "customer-cart-item");
            const itemImage = makeElement("img", "customer-cart-item-image");
            itemImage.src = entry.image;
            itemImage.alt = entry.name;

            const itemBody = makeElement("div", "customer-cart-item-body");
            const name = makeElement("h3", "customer-cart-item-name", entry.name);
            itemBody.appendChild(name);

            const optionText = buildSelectionText(entry);
            if (optionText) {
                itemBody.appendChild(makeElement("p", "customer-cart-item-option", optionText));
            }

            if (entry.remarks) {
                itemBody.appendChild(makeElement("p", "customer-cart-item-remark", `Remark: ${entry.remarks}`));
            }

            const controls = makeElement("div", "customer-cart-item-controls");
            const decreaseButton = makeElement("button", "customer-cart-qty-btn", "");
            decreaseButton.type = "button";
            decreaseButton.setAttribute("data-cart-index", String(index));
            decreaseButton.setAttribute("data-cart-action", "decrease");
            if (entry.quantity > 1) {
                decreaseButton.textContent = "-";
            } else {
                decreaseButton.classList.add("customer-cart-remove-btn");
                decreaseButton.innerHTML = '<i class="fa-solid fa-trash" aria-hidden="true"></i>';
            }

            const quantityDisplay = makeElement("span", "customer-cart-quantity", String(entry.quantity));
            const increaseButton = makeElement("button", "customer-cart-qty-btn", "+");
            increaseButton.type = "button";
            increaseButton.setAttribute("data-cart-index", String(index));
            increaseButton.setAttribute("data-cart-action", "increase");

            controls.append(decreaseButton, quantityDisplay, increaseButton);
            row.append(itemImage, itemBody, controls);
            cartItemsContainer.appendChild(row);
        });
    }

    function updateAdjustment(action, index) {
        const item = cart[index];
        if (!item) return;

        if (action === "increase") item.quantity += 1;
        if (action === "decrease") {
            if (item.quantity > 1) {
                item.quantity -= 1;
            } else {
                cart.splice(index, 1);
            }
        }

        updateCartSummary();
    }

    function toggleCart(forceOpen) {
        const shouldOpen = typeof forceOpen === "boolean" ? forceOpen : !cartPanel.classList.contains("open");
        cartPanel.classList.toggle("open", shouldOpen);
        cartPanel.setAttribute("aria-hidden", String(!shouldOpen));
        cartOverlay.hidden = !shouldOpen;
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

    if (tableSelect) {
        tableSelect.addEventListener("change", validateOrderButton);
    }

    quantityMinus.addEventListener("click", () => {
        if (currentQuantity > 1) {
            currentQuantity -= 1;
        } else if (currentDraftItem && currentDraftItem.item) {
            closeDetails();
            return;
        }
        updateQuantityUI();
    });

    quantityPlus.addEventListener("click", () => {
        currentQuantity += 1;
        updateQuantityUI();
    });

    orderRemarks.addEventListener("input", (event) => {
        if (!currentDraftItem) return;
        currentDraftItem.remarks = event.target.value;
    });

    quantityValue.textContent = String(currentQuantity);

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

    cartButton.addEventListener("click", (event) => {
        if (cartButtonWasDragged) {
            cartButtonWasDragged = false;
            event.preventDefault();
            return;
        }
        toggleCart();
    });

    cartOverlay.addEventListener("click", () => toggleCart(false));
    cartCloseButton.addEventListener("click", () => toggleCart(false));

    // CONFIRM ORDER SUBMISSION HANDLER
    confirmOrderButton.addEventListener("click", async () => {
        if (cart.length === 0) {
            validateOrderButton();
            return;
        }

        const selectedTableId = tableSelect ? tableSelect.value : "";
        if (!selectedTableId) {
            window.alert("Please select a dine-in table before sending your order.");
            if (tableSelect) tableSelect.focus();
            return;
        }

        confirmOrderButton.disabled = true;
        confirmOrderButton.textContent = "Placing Order...";

        // Map cart array to match backend DTO
        const orderItemsPayload = cart.map(item => {
            const customText = buildSelectionText(item);
            let combinedRemarks = customText;
            if (item.remarks) {
                combinedRemarks = combinedRemarks ? `${combinedRemarks} | ${item.remarks}` : item.remarks;
            }

            return {
                foodId: item.foodId,
                quantity: item.quantity,
                remarks: combinedRemarks || null
            };
        });

        const payload = {
            tableId: selectedTableId,
            bookingId: activeBooking ? activeBooking.bookingId : null,
            items: orderItemsPayload
        };

        const token = document.getElementById("__RequestVerificationToken")?.value || "";

        try {
            const response = await fetch("/Customer/Menus?handler=CreateOrder", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": token
                },
                body: JSON.stringify(payload)
            });

            const result = await response.json();

            if (response.ok && result.success) {
                window.alert(`Order successfully placed! Order ID: ${result.orderId} (Status: Pending)`);

                // Clear local cart and close panel
                cart.length = 0;
                updateCartSummary();
                toggleCart(false);
            } else {
                window.alert(`Failed to place order: ${result.message || "Unknown error"}`);
            }
        } catch (err) {
            window.alert("A network error occurred while placing your order.");
            console.error(err);
        } finally {
            confirmOrderButton.disabled = false;
            confirmOrderButton.textContent = "Confirm Order";
        }
    });

    cartItemsContainer.addEventListener("click", (event) => {
        const target = event.target.closest("button[data-cart-action]");
        if (!target) return;

        const index = Number(target.dataset.cartIndex);
        const action = target.dataset.cartAction;
        updateAdjustment(action, index);
    });

    cartButton.addEventListener("pointerdown", (event) => {
        cartButtonDragState = {
            startX: event.clientX,
            startY: event.clientY,
            startLeft: cartButton.offsetLeft || 0,
            startTop: cartButton.offsetTop || 0
        };
    });

    cartButton.addEventListener("pointermove", (event) => {
        if (!cartButtonDragState) return;

        const deltaX = event.clientX - cartButtonDragState.startX;
        const deltaY = event.clientY - cartButtonDragState.startY;
        if (Math.abs(deltaX) > 8 || Math.abs(deltaY) > 8) {
            cartButtonWasDragged = true;
        }

        const boundX = Math.min(Math.max(cartButtonDragState.startLeft + deltaX, 16), window.innerWidth - cartButton.offsetWidth - 20);
        const boundY = Math.min(Math.max(cartButtonDragState.startTop + deltaY, 16), window.innerHeight - cartButton.offsetHeight - 20);

        cartButton.style.left = `${boundX}px`;
        cartButton.style.top = `${boundY}px`;
        cartButton.style.right = "auto";
        cartButton.style.bottom = "auto";
    });

    cartButton.addEventListener("pointerup", () => {
        cartButtonDragState = null;
    });

    cartButton.addEventListener("pointercancel", () => {
        cartButtonDragState = null;
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            if (!modal.hidden) closeDetails();
            if (cartPanel.classList.contains("open")) toggleCart(false);
            return;
        }

        if (modal.hidden) return;
        if (event.key === "Tab") {
            event.preventDefault();
            modal.querySelector("[data-close-modal]").focus();
        }
    });

    populateTableOptions();
    updateCartSummary();
    renderRecommendations();
    renderMenu();
})();
(() => {
    const menuItems = window.menuItemsData || [];
    const canPlaceOrder = window.canPlaceOrder === true;
    const activePromotions = window.activePromotionsData || [];
    const availableTables = window.availableTablesData || [];
    const activeBooking = window.activeBookingData || null;

    const promoMap = new Map();
    activePromotions.forEach((p) => {
        if (p.foodId) {
            promoMap.set(String(p.foodId).toLowerCase(), p);
        }
    });

    function getPromo(item) {
        if (!item) return null;
        const id = String(item.foodID || item.foodId || "").toLowerCase();
        return promoMap.get(id) || null;
    }

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
    const quantityValue = document.getElementById("customerQtyValue");
    const quantityPlus = document.getElementById("customerQtyPlus");
    const quantityMinus = document.getElementById("customerQtyMinus");
    const orderRemarks = document.getElementById("customerOrderRemarks");
    const orderCustomizations = document.getElementById("customerOrderCustomizations");
    const addOrderButton = document.getElementById("customerAddOrderBtn");
    const cartCloseButton = document.getElementById("customerCartClose");

    const modalOrigPrice = document.getElementById("customerModalOrigPrice");
    const modalPromoBanner = document.getElementById("customerModalPromoBanner");
    const modalPromoText = document.getElementById("customerModalPromoBannerText");

    const cartOrigRow = document.getElementById("customerCartOrigRow");
    const cartOrigSubtotal = document.getElementById("customerCartOrigSubtotal");
    const cartDiscountRow = document.getElementById("customerCartDiscountRow");
    const cartDiscount = document.getElementById("customerCartDiscount");
    const cartSavingsBanner = document.getElementById("customerCartSavingsBanner");
    const cartSavingsValue = document.getElementById("customerCartSavingsValue");

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

    function validateOrderButton() {
        const hasItems = cart.length > 0;
        confirmOrderButton.disabled = !hasItems || !canPlaceOrder;
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
        const promo = getPromo(item);

        const imageWrap = makeElement("div", "customer-food-image-wrap");
        const categoryTag = makeElement("span", "customer-category-tag", item.category || "Main Course");
        imageWrap.append(makeImage(item, "customer-food-image"), categoryTag);

        // Make the image container interactive
        imageWrap.setAttribute("role", "button");
        imageWrap.setAttribute("tabindex", "0");
        imageWrap.setAttribute("aria-label", `View details for ${item.foodName || "dish"}`);
    
        const openModalHandler = (e) => {
            e.stopPropagation();
            openOrderModal(item, addOrderButtonCard);
        };

        imageWrap.addEventListener("click", openModalHandler);
        imageWrap.addEventListener("keydown", (e) => {
            if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                openModalHandler(e);
            }
        });

        const content = makeElement("div", "customer-food-content");

        if (promo) {
            const promoPill = makeElement("span", "customer-promo-pill");
            promoPill.innerHTML = `<i class="fa-solid fa-tag" aria-hidden="true"></i> ${promo.discountPercent}% OFF`;
            content.appendChild(promoPill);
        }

        const titleRow = makeElement("div", "customer-food-title-row");
        const titleH3 = makeElement("h3", "", item.foodName || "Unnamed Dish");

        if (promo) {
            const priceWrap = makeElement("div", "customer-food-price-wrap");
            const origPrice = makeElement("del", "customer-card-orig-price", formatPrice(item.price));
            const dealPrice = makeElement("span", "customer-food-price customer-card-deal-price", formatPrice(promo.discountedPrice));
            priceWrap.append(origPrice, dealPrice);
            titleRow.append(titleH3, priceWrap);
        } else {
            titleRow.append(
                titleH3,
                makeElement("span", "customer-food-price", formatPrice(item.price))
            );
        }

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

        const promo = getPromo(item);

        const details = makeElement("div", "customer-recommendation-details");
        details.appendChild(makeElement("span", "customer-recommendation-label", item.recommendation));
        details.appendChild(makeElement("h3", "", item.foodName));

        if (promo) {
            const priceWrap = makeElement("div", "customer-recommendation-price-wrap");
            priceWrap.append(
                makeElement("del", "customer-card-orig-price", formatPrice(item.price)),
                makeElement("span", "customer-recommendation-price customer-card-deal-price", formatPrice(promo.discountedPrice))
            );
            details.appendChild(priceWrap);
        } else {
            details.appendChild(makeElement("span", "customer-recommendation-price", formatPrice(item.price)));
        }

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

        filteredItems.sort((a, b) => (a.foodName || "").localeCompare(b.foodName || ""));

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
        const promo = getPromo(item);

        document.getElementById("customerModalImage").src = item.imageUrl || "/images/placeholder-food.jpg";
        document.getElementById("customerModalImage").alt = item.imageAlt || item.foodName;
        document.getElementById("customerModalCategory").textContent = item.category || "Main Course";
        document.getElementById("customerModalTitle").textContent = item.foodName;

        if (promo) {
            if (modalOrigPrice) {
                modalOrigPrice.textContent = formatPrice(item.price);
                modalOrigPrice.hidden = false;
            }
            document.getElementById("customerModalPrice").textContent = formatPrice(promo.discountedPrice);
            if (modalPromoBanner && modalPromoText) {
                modalPromoBanner.hidden = false;
                modalPromoText.textContent = `Special Promotion! Save ${formatPrice(promo.discountAmount)} (${promo.discountPercent}% off)`;
            }
        } else {
            if (modalOrigPrice) modalOrigPrice.hidden = true;
            document.getElementById("customerModalPrice").textContent = formatPrice(item.price);
            if (modalPromoBanner) modalPromoBanner.hidden = true;
        }

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
        const promo = getPromo(item);
        const origPrice = Number(item.price) || 0;
        const effectivePrice = promo ? Number(promo.discountedPrice) : origPrice;

        const newSelection = {
            foodId: item.foodID || item.foodId,
            name: item.foodName,
            image: item.imageUrl || "/images/placeholder-food.jpg",
            price: effectivePrice,
            originalPrice: origPrice,
            hasPromo: !!promo,
            discountAmount: promo ? Math.max(0, origPrice - effectivePrice) : 0,
            discountPercent: promo ? promo.discountPercent : 0,
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
        const origSubtotal = cart.reduce((total, item) => total + (item.originalPrice || item.price) * Number(item.quantity || 0), 0);
        const netSubtotal = cart.reduce((total, item) => total + Number(item.price || 0) * Number(item.quantity || 0), 0);
        const totalDiscount = Math.max(0, origSubtotal - netSubtotal);
        const tax = netSubtotal * 0.1;
        const total = netSubtotal + tax;

        cartSubtotal.textContent = formatPrice(netSubtotal);
        cartTax.textContent = formatPrice(tax);
        cartTotal.textContent = formatPrice(total);

        if (totalDiscount > 0) {
            if (cartOrigRow) cartOrigRow.hidden = false;
            if (cartOrigSubtotal) cartOrigSubtotal.textContent = formatPrice(origSubtotal);
            if (cartDiscountRow) cartDiscountRow.hidden = false;
            if (cartDiscount) cartDiscount.textContent = `- ${formatPrice(totalDiscount)}`;
            if (cartSavingsBanner) cartSavingsBanner.hidden = false;
            if (cartSavingsValue) cartSavingsValue.textContent = formatPrice(totalDiscount);
        } else {
            if (cartOrigRow) cartOrigRow.hidden = true;
            if (cartDiscountRow) cartDiscountRow.hidden = true;
            if (cartSavingsBanner) cartSavingsBanner.hidden = true;
        }

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

            // Left Column: Image
            const itemImage = makeElement("img", "customer-cart-item-image");
            itemImage.src = entry.image;
            itemImage.alt = entry.name;

            // Middle Column: Food Name & Options/Remarks
            const itemBody = makeElement("div", "customer-cart-item-body");
            const name = makeElement("h3", "customer-cart-item-name", entry.name);
            if (entry.hasPromo) {
                const promoBadge = makeElement("span", "cart-promo-badge", "PROMO");
                name.appendChild(promoBadge);
            }
            itemBody.appendChild(name);

            const optionText = buildSelectionText(entry);
            if (optionText) {
                itemBody.appendChild(makeElement("p", "customer-cart-item-option", optionText));
            }

            if (entry.remarks) {
                itemBody.appendChild(makeElement("p", "customer-cart-item-remark", `Remark: ${entry.remarks}`));
            }

            // Right Column: Price and Stepper
            const itemSide = makeElement("div", "customer-cart-item-side");
            const itemTotalPrice = entry.price * entry.quantity;

            const priceDisplay = makeElement("div", "customer-cart-item-price-wrap");
            if (entry.hasPromo) {
                const origTotalPrice = entry.originalPrice * entry.quantity;
                const origPriceEl = makeElement("del", "cart-orig-price", formatPrice(origTotalPrice));
                const dealPriceEl = makeElement("span", "customer-cart-item-price cart-deal-price", formatPrice(itemTotalPrice));
                priceDisplay.append(origPriceEl, dealPriceEl);
                const savingsEl = makeElement("span", "cart-line-savings", `Save ${formatPrice(origTotalPrice - itemTotalPrice)}`);
                priceDisplay.appendChild(savingsEl);
            } else {
                const priceEl = makeElement("span", "customer-cart-item-price", formatPrice(itemTotalPrice));
                priceDisplay.appendChild(priceEl);
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
            itemSide.append(priceDisplay, controls);

            // Assemble row
            row.append(itemImage, itemBody, itemSide);
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

    confirmOrderButton.addEventListener("click", async () => {
        if (cart.length === 0) {
            validateOrderButton();
            return;
        }

        if (!canPlaceOrder) {
            window.alert("Please scan the QR code at your table or open your active booking before placing an order.");
            return;
        }

        const selectedPaymentMethod = document.querySelector('input[name="paymentMethod"]:checked')?.value;
        if (!selectedPaymentMethod) {
            window.alert("Please select a payment method before placing your order.");
            return;
        }

        confirmOrderButton.disabled = true;
        confirmOrderButton.textContent = "Placing Order...";

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
            items: orderItemsPayload,
            paymentMethod: selectedPaymentMethod
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

            const contentType = response.headers.get("content-type") || "";
            if (!contentType.includes("application/json")) {
                window.alert("Your sign-in may have expired. Please sign in and scan the table QR code again.");
                return;
            }

            const result = await response.json();

            if (response.ok && result.success) {
                window.alert(`Order successfully placed! Order ID: ${result.orderId} (Status: Pending)`);

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


    let animFrameId = null;
    let currentX = 0;
    let currentY = 0;

    cartButton.addEventListener("pointerdown", (event) => {
        // Capture pointer events so fast movement stays locked to this button
        cartButton.setPointerCapture(event.pointerId);

        const rect = cartButton.getBoundingClientRect();
        cartButtonDragState = {
            pointerId: event.pointerId,
            startX: event.clientX,
            startY: event.clientY,
            startLeft: rect.left,
            startTop: rect.top
        };
        currentX = rect.left;
        currentY = rect.top;
    });

    cartButton.addEventListener("pointermove", (event) => {
        if (!cartButtonDragState) return;

        const deltaX = event.clientX - cartButtonDragState.startX;
        const deltaY = event.clientY - cartButtonDragState.startY;

        // Register drag if moved more than 8 pixels
        if (Math.abs(deltaX) > 8 || Math.abs(deltaY) > 8) {
            cartButtonWasDragged = true;
        }

        // Clamp bounds within viewport padding (16px)
        const boundX = Math.min(Math.max(cartButtonDragState.startLeft + deltaX, 16), window.innerWidth - cartButton.offsetWidth - 16);
        const boundY = Math.min(Math.max(cartButtonDragState.startTop + deltaY, 16), window.innerHeight - cartButton.offsetHeight - 16);

        currentX = boundX;
        currentY = boundY;

        // Use RequestAnimationFrame for smooth 60fps rendering without lag
        if (!animFrameId) {
            animFrameId = requestAnimationFrame(() => {
                cartButton.style.left = `${currentX}px`;
                cartButton.style.top = `${currentY}px`;
                cartButton.style.right = "auto";
                cartButton.style.bottom = "auto";
                animFrameId = null;
            });
        }
    });

    function handlePointerEnd(event) {
        if (!cartButtonDragState) return;

        if (cartButton.hasPointerCapture(event.pointerId)) {
            cartButton.releasePointerCapture(event.pointerId);
        }
        cartButtonDragState = null;
        if (animFrameId) {
            cancelAnimationFrame(animFrameId);
            animFrameId = null;
        }
    }

    cartButton.addEventListener("pointerup", handlePointerEnd);
    cartButton.addEventListener("pointercancel", handlePointerEnd);

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


    let statusCheckInterval = null;

    async function checkTableSessionStatus() {
        try {
            const response = await fetch("/Customer/Menus?handler=CheckTableSessionStatus", {
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) return;

            const data = await response.json();
            if (!data.valid) {
                window.canPlaceOrder = false;
                validateOrderButton();

                const statusElement = document.querySelector(".customer-table-session-status");
                if (statusElement) {
                    statusElement.innerHTML = `<span>${data.message || "Please scan the QR code at your table to start an order."}</span>`;
                }

                const cartLabelElement = document.querySelector(".customer-cart-table-label");
                if (cartLabelElement) {
                    cartLabelElement.innerHTML = `<span>Scan the QR code at your table to order.</span>`;
                }
            }
        } catch (err) {
            console.warn("Table session status check failed:", err);
        }
    }

    // Check status on page load, tab visibility change, and every 10 seconds
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") {
            checkTableSessionStatus();
        }
    });

    statusCheckInterval = setInterval(checkTableSessionStatus, 10000);

    updateCartSummary();
    renderRecommendations();
    renderMenu();
})();

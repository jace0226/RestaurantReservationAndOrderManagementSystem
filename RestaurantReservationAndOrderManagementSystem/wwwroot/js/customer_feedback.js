document.addEventListener("DOMContentLoaded", () => {
    const form = document.getElementById("customerFeedbackForm");
    const ratingInput = document.getElementById("feedbackRating");
    const messageInput = document.getElementById("feedbackMessage");
    const ratingValidation = document.getElementById("ratingValidation");
    const messageValidation = document.getElementById("messageValidation");
    const submitButton = document.getElementById("submitFeedbackButton");
    const starButtons = Array.from(document.querySelectorAll(".customer-feedback-star"));

    if (
        !(form instanceof HTMLFormElement) ||
        !(ratingInput instanceof HTMLInputElement) ||
        !(messageInput instanceof HTMLTextAreaElement) ||
        !(ratingValidation instanceof HTMLElement) ||
        !(messageValidation instanceof HTMLElement) ||
        !(submitButton instanceof HTMLButtonElement)
    ) {
        return;
    }

    const updateStars = (rating) => {
        ratingInput.value = rating ? String(rating) : "";
        const starsContainer = document.querySelector(".customer-feedback-stars");

        for (const button of starButtons) {
            const buttonRating = Number(button.dataset.rating);
            const isFilled = buttonRating <= rating;
            
            button.textContent = isFilled ? "★" : "☆";
            button.classList.toggle("is-filled", isFilled);
            button.setAttribute("aria-pressed", String(buttonRating === rating));
        }
        starsContainer?.setAttribute("aria-invalid", "false");
    };

    const showHoverRating = (rating) => {
        const selectedRating = Number(ratingInput.value) || 0;
        
        if (rating === 0) {
            updateStars(selectedRating);
            for (const button of starButtons) {
                button.classList.remove("is-hovered");
            }
            return;
        }

        for (const button of starButtons) {
            const buttonRating = Number(button.dataset.rating);
            const isHovered = buttonRating <= rating;
            button.classList.toggle("is-hovered", isHovered);
            button.textContent = isHovered ? "★" : "☆";
        }
    };

    for (const button of starButtons) {
        button.addEventListener("mouseenter", () => {
            showHoverRating(Number(button.dataset.rating));
        });

        button.addEventListener("click", () => {
            const rating = Number(button.dataset.rating);
            updateStars(rating);
            showHoverRating(0);
            ratingValidation.textContent = "";
        });
    }

    document.querySelector(".customer-feedback-stars")?.addEventListener("mouseleave", () => {
        showHoverRating(0);
    });

    // Sync initial state on load (if server rendered a selected value)
    const initialRating = Number(ratingInput.value) || 0;
    if (initialRating > 0) {
        updateStars(initialRating);
    }

    messageInput.addEventListener("input", () => {
        if (messageInput.value.trim()) {
            messageValidation.textContent = "";
        }
    });

    form.addEventListener("submit", (event) => {
        messageInput.value = messageInput.value.trim();

        let isValid = true;
        if (!/^[1-5]$/.test(ratingInput.value)) {
            ratingValidation.textContent = "Please select a star rating.";
            document.querySelector(".customer-feedback-stars")?.setAttribute("aria-invalid", "true");
            starButtons[0]?.focus();
            isValid = false;
        }

        if (!messageInput.value) {
            messageValidation.textContent = "Please enter your feedback message.";
            messageInput.setAttribute("aria-invalid", "true");
            if (isValid) {
                messageInput.focus();
            }
            isValid = false;
        } else {
            messageInput.removeAttribute("aria-invalid");
        }

        if (!isValid) {
            event.preventDefault();
            return;
        }

        submitButton.disabled = true;
        submitButton.textContent = "Submitting...";
    });
});
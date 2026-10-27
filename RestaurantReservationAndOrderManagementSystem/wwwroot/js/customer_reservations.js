document.addEventListener('DOMContentLoaded', () => {
    const modal = document.getElementById('customerReservationModal');
    const form = document.getElementById('customerReservationForm');
    const openButton = document.getElementById('openReservationModalBtn');
    const closeButtons = document.querySelectorAll('[data-close-reservation-modal]');
    const editButtons = document.querySelectorAll('.open-edit-button');
    const bookingIdInput = document.getElementById('BookingId');
    const bookingDateInput = document.getElementById('ReservationForm_BookingDate');
    const bookingTimeSelect = document.getElementById('ReservationForm_BookingTime');
    const partySizeInput = document.getElementById('ReservationForm_PartySize');
    const bookingNotesInput = document.getElementById('ReservationForm_BookingNotes');
    const submitButton = document.getElementById('submitReservationButton');

    if (!modal || !form) {
        return;
    }

    const modalBaseUrl = form.action || '';

    // Function to fetch available slots dynamically for the specific date selected
    async function updateAvailableTimeSlots(preferredSelectedTime = null) {
        if (!bookingDateInput || !bookingTimeSelect) return;

        const selectedDate = bookingDateInput.value;
        const currentBookingId = bookingIdInput ? bookingIdInput.value : '';

        if (!selectedDate) return;

        // Remember current dropdown value if none explicitly passed
        const targetTime = preferredSelectedTime !== null ? preferredSelectedTime : bookingTimeSelect.value;

        try {
            // Added timestamp parameter (_t) to prevent browser HTTP caching
            const url = `?handler=AvailableTimeSlots&selectedDate=${encodeURIComponent(selectedDate)}&bookingId=${encodeURIComponent(currentBookingId)}&_t=${Date.now()}`;
            const response = await fetch(url, { headers: { 'Accept': 'application/json' } });

            if (!response.ok) return;

            const slots = await response.json();

            // Clear existing dropdown options
            bookingTimeSelect.innerHTML = '<option value="">Select a time</option>';

            // Populate options with available time slots for selectedDate
            slots.forEach(slot => {
                const option = document.createElement('option');
                option.value = slot.value;
                option.textContent = slot.text;

                if (slot.value === targetTime) {
                    option.selected = true;
                }

                bookingTimeSelect.appendChild(option);
            });
        } catch (error) {
            console.error("Error fetching available time slots:", error);
        }
    }

    // Trigger dynamic fetch whenever the user changes the date picker
    if (bookingDateInput) {
        bookingDateInput.addEventListener('change', () => {
            updateAvailableTimeSlots();
        });
    }

    function openModal(mode = 'create') {
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        const baseUrl = modalBaseUrl.split('?')[0];
        form.setAttribute('action', mode === 'edit' ? `${baseUrl}?handler=Edit` : `${baseUrl}?handler=Create`);

        if (submitButton) {
            submitButton.textContent = mode === 'edit' ? 'Update Reservation' : 'Submit Reservation';
        }

        if (mode === 'create') {
            if (bookingIdInput) bookingIdInput.value = '';

            // Set default date to tomorrow if empty
            if (bookingDateInput && !bookingDateInput.value) {
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                bookingDateInput.value = tomorrow.toISOString().slice(0, 10);
            }
            if (partySizeInput && !partySizeInput.value) partySizeInput.value = '2';
            if (bookingNotesInput) bookingNotesInput.value = '';
            if (bookingTimeSelect) bookingTimeSelect.value = '';

            // Fetch available time slots for the defaulted date
            updateAvailableTimeSlots();
        }
    }

    function closeModal() {
        modal.classList.remove('is-open');
        modal.setAttribute('aria-hidden', 'true');
    }

    if (openButton) {
        openButton.addEventListener('click', () => openModal('create'));
    }

    closeButtons.forEach(button => {
        button.addEventListener('click', closeModal);
    });

    modal.addEventListener('click', (event) => {
        if (event.target === modal) closeModal();
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && modal.classList.contains('is-open')) closeModal();
    });

    editButtons.forEach(button => {
        button.addEventListener('click', () => {
            const bookingId = button.dataset.bookingId || '';
            const bookingDate = button.dataset.bookingDate || '';
            const bookingTime = button.dataset.bookingTime || '';
            const partySize = button.dataset.partySize || '2';
            const bookingNotes = button.dataset.bookingNotes || '';

            if (bookingIdInput) bookingIdInput.value = bookingId;
            if (bookingDateInput) bookingDateInput.value = bookingDate;
            if (partySizeInput) partySizeInput.value = partySize;
            if (bookingNotesInput) bookingNotesInput.value = bookingNotes;

            openModal('edit');

            // Fetch available time slots for this booking's date and keep the current booked time selected
            updateAvailableTimeSlots(bookingTime);
        });
    });

    if (modal.classList.contains('is-open')) {
        modal.setAttribute('aria-hidden', 'false');
    }
});
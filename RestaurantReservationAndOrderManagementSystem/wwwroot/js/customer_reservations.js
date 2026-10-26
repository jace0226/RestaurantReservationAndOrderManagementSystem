document.addEventListener('DOMContentLoaded', () => {
    const modal = document.getElementById('customerReservationModal');
    const form = document.getElementById('customerReservationForm');
    const openButton = document.getElementById('openReservationModalBtn');
    const closeButtons = document.querySelectorAll('[data-close-reservation-modal]');
    const editButtons = document.querySelectorAll('.open-edit-button');
    const bookingIdInput = document.getElementById('BookingId');
    const bookingDateInput = document.getElementById('ReservationForm_BookingDate');
    const bookingTimeInput = document.getElementById('ReservationForm_BookingTime');
    const partySizeInput = document.getElementById('ReservationForm_PartySize');
    const bookingNotesInput = document.getElementById('ReservationForm_BookingNotes');
    const submitButton = document.getElementById('submitReservationButton'); // 1. Select the submit button

    if (!modal || !form) {
        return;
    }

    const modalBaseUrl = form.action || '';

    function openModal(mode = 'create') {
        modal.classList.add('is-open');
        modal.setAttribute('aria-hidden', 'false');
        const baseUrl = modalBaseUrl.split('?')[0];
        form.setAttribute('action', mode === 'edit' ? `${baseUrl}?handler=Edit` : `${baseUrl}?handler=Create`);

        // 2. Dynamically update the submit button text based on mode
        if (submitButton) {
            submitButton.textContent = mode === 'edit' ? 'Update Reservation' : 'Submit Reservation';
        }

        if (mode === 'create') {
            if (bookingIdInput) {
                bookingIdInput.value = '';
            }
            if (bookingDateInput && !bookingDateInput.value) {
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                bookingDateInput.value = tomorrow.toISOString().slice(0, 10);
            }
            if (partySizeInput && !partySizeInput.value) {
                partySizeInput.value = '2';
            }
            if (bookingNotesInput) {
                bookingNotesInput.value = '';
            }
            if (bookingTimeInput) {
                bookingTimeInput.value = '';
            }
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
        if (event.target === modal) {
            closeModal();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && modal.classList.contains('is-open')) {
            closeModal();
        }
    });

    editButtons.forEach(button => {
        button.addEventListener('click', () => {
            const bookingId = button.dataset.bookingId || '';
            const bookingDate = button.dataset.bookingDate || '';
            const bookingTime = button.dataset.bookingTime || '';
            const partySize = button.dataset.partySize || '2';
            const bookingNotes = button.dataset.bookingNotes || '';

            if (bookingIdInput) {
                bookingIdInput.value = bookingId;
            }
            if (bookingDateInput) {
                bookingDateInput.value = bookingDate;
            }
            if (bookingTimeInput) {
                bookingTimeInput.value = bookingTime;
            }
            if (partySizeInput) {
                partySizeInput.value = partySize;
            }
            if (bookingNotesInput) {
                bookingNotesInput.value = bookingNotes;
            }

            openModal('edit');
        });
    });

    if (modal.classList.contains('is-open')) {
        modal.setAttribute('aria-hidden', 'false');
    }
});
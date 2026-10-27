using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using BookingEntity = RestaurantReservationAndOrderManagementSystem.Models.Booking;
using CustomerEntity = RestaurantReservationAndOrderManagementSystem.Models.Customer;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    [Authorize(Roles = "Customer")]
    public class CustomerReservationsModel : PageModel
    {
        private const string DefaultCategory = "Upcoming";
        private static readonly IReadOnlyList<TimeOnly> AllReservationSlots = BuildTimeSlots();
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CustomerReservationsModel> _logger;

        public CustomerReservationsModel(
            ApplicationDbContext dbContext,
            ILogger<CustomerReservationsModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Category { get; set; } = DefaultCategory;

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateFilter { get; set; }

        [BindProperty]
        public ReservationFormInput ReservationForm { get; set; } = new();

        [BindProperty]
        public string? BookingId { get; set; }

        [TempData]
        public string? ReservationSuccess { get; set; }

        [TempData]
        public string? ReservationError { get; set; }

        public IReadOnlyList<ReservationItem> Reservations { get; private set; } = [];

        public List<TimeOnly> TimeSlots { get; private set; } = new();

        public string MinimumReservationDate => DateOnly.FromDateTime(DateTime.Today).AddDays(1).ToString("yyyy-MM-dd");

        public bool IsModalOpen { get; private set; }

        public bool IsEditMode { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!IsValidCategory(Category))
            {
                Category = DefaultCategory;
            }

            if (ReservationForm.BookingDate is null)
            {
                ReservationForm.BookingDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
            }

            await LoadReservationsAsync();
            await PopulateAvailableTimeSlotsAsync();
            return Page();
        }

        /// <summary>
        /// AJAX Endpoint: Returns available time slots as JSON when user changes the date picker on front-end
        /// </summary>
        public async Task<JsonResult> OnGetAvailableTimeSlotsAsync(DateOnly selectedDate, string? bookingId)
        {
            var customer = await GetCurrentCustomerAsync();
            var allSlots = BuildTimeSlots();

            if (customer is null)
            {
                return new JsonResult(allSlots.Select(s => new { value = s.ToString("HH:mm"), text = s.ToString("h:mm tt") }));
            }

            var bookedTimes = await _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerID == customer.CustomerID
                         && b.BookingDate == selectedDate
                         && b.BookingStatus != "Cancelled"
                         && (string.IsNullOrEmpty(bookingId) || b.BookingID != bookingId))
                .Select(b => b.BookingTime)
                .ToListAsync();

            var availableSlots = allSlots
                .Where(slot => !bookedTimes.Contains(slot))
                .Select(slot => new { value = slot.ToString("HH:mm"), text = slot.ToString("h:mm tt") });

            return new JsonResult(availableSlots);
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            IsModalOpen = true;
            IsEditMode = false;
            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                return NotFound();
            }

            ModelState.Clear();
            if (!TryValidateModel(ReservationForm, nameof(ReservationForm)))
            {
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            var result = await SaveReservationAsync(customer, isUpdate: false);
            if (result is not null)
            {
                return result;
            }

            await LoadReservationsAsync();
            await PopulateAvailableTimeSlotsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            IsModalOpen = true;
            IsEditMode = true;
            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                return NotFound();
            }

            ModelState.Clear();
            if (!TryValidateModel(ReservationForm, nameof(ReservationForm)))
            {
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            var booking = await _dbContext.Bookings
                .SingleOrDefaultAsync(item => item.BookingID == BookingId && item.CustomerID == customer.CustomerID);

            if (booking is null)
            {
                ReservationError = "That reservation could not be found.";
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            if (!string.Equals(booking.BookingStatus, "Waiting", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Only waiting reservations can be modified.");
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            var reservationDate = ReservationForm.BookingDate!.Value;
            var reservationTime = ReservationForm.BookingTime!.Value;

            if (!ValidateReservationInput(reservationDate, reservationTime, out var validationMessage))
            {
                ModelState.AddModelError(string.Empty, validationMessage);
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            if (await IsTimeSlotBookedByUserAsync(customer.CustomerID, reservationDate, reservationTime, BookingId))
            {
                ModelState.AddModelError(string.Empty, "You already have an active reservation for this date and time.");
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            booking.BookingDate = reservationDate;
            booking.BookingTime = reservationTime;
            booking.PartySize = ReservationForm.PartySize;
            booking.BookingNotes = ReservationForm.BookingNotes?.Trim() ?? string.Empty;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not update the reservation for customer {CustomerId}", customer.CustomerID);
                ModelState.AddModelError(string.Empty, "We could not save your reservation changes. Please try again.");
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            ReservationSuccess = "Your reservation has been updated.";
            return RedirectToPage(new { category = Category, dateFilter = DateFilter?.ToString("yyyy-MM-dd") });
        }

        public async Task<IActionResult> OnPostCancelAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(BookingId))
            {
                ReservationError = "A reservation ID is required to cancel this booking.";
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            var booking = await _dbContext.Bookings
                .SingleOrDefaultAsync(item => item.BookingID == BookingId && item.CustomerID == customer.CustomerID);

            if (booking is null)
            {
                ReservationError = "That reservation could not be found.";
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var canCancel = (booking.BookingStatus == "Waiting" || booking.BookingStatus == "Confirmed") &&
                booking.BookingDate > today;

            if (!canCancel)
            {
                ReservationError = "This reservation can no longer be cancelled.";
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            booking.BookingStatus = "Cancelled";

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not cancel the reservation {BookingId}", booking.BookingID);
                ReservationError = "We could not cancel this reservation. Please try again.";
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            ReservationSuccess = "Your reservation has been cancelled.";
            return RedirectToPage(new { category = Category, dateFilter = DateFilter?.ToString("yyyy-MM-dd") });
        }

        private async Task<IActionResult?> SaveReservationAsync(CustomerEntity customer, bool isUpdate)
        {
            var reservationDate = ReservationForm.BookingDate!.Value;
            var reservationTime = ReservationForm.BookingTime!.Value;

            if (!ValidateReservationInput(reservationDate, reservationTime, out var validationMessage))
            {
                ModelState.AddModelError(string.Empty, validationMessage);
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            if (await IsTimeSlotBookedByUserAsync(customer.CustomerID, reservationDate, reservationTime, BookingId))
            {
                ModelState.AddModelError(string.Empty, "You already have an active reservation for this date and time.");
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            if (isUpdate)
            {
                return null;
            }

            var newBookingId = await GenerateNextBookingIdAsync();
            var booking = new BookingEntity
            {
                BookingID = newBookingId,
                CustomerID = customer.CustomerID,
                BookingDate = reservationDate,
                BookingTime = reservationTime,
                PartySize = ReservationForm.PartySize,
                BookingStatus = "Waiting",
                BookingNotes = ReservationForm.BookingNotes?.Trim() ?? string.Empty,
                TableID = null
            };

            _dbContext.Bookings.Add(booking);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not save the new reservation for customer {CustomerId}", customer.CustomerID);
                ModelState.AddModelError(string.Empty, "We could not save your reservation. Please try again.");
                await LoadReservationsAsync();
                await PopulateAvailableTimeSlotsAsync();
                return Page();
            }

            ReservationSuccess = "Your reservation has been submitted.";
            return RedirectToPage(new { category = Category, dateFilter = DateFilter?.ToString("yyyy-MM-dd") });
        }

        /// <summary>
        /// Server-side helper to populate TimeSlots property during initial page render
        /// </summary>
        private async Task PopulateAvailableTimeSlotsAsync()
        {
            var customer = await GetCurrentCustomerAsync();

            if (customer is null || !ReservationForm.BookingDate.HasValue)
            {
                TimeSlots = AllReservationSlots.ToList();
                return;
            }

            var selectedDate = ReservationForm.BookingDate.Value;

            var bookedTimes = await _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerID == customer.CustomerID
                         && b.BookingDate == selectedDate
                         && b.BookingStatus != "Cancelled"
                         && (string.IsNullOrEmpty(BookingId) || b.BookingID != BookingId))
                .Select(b => b.BookingTime)
                .ToListAsync();

            TimeSlots = AllReservationSlots.Where(slot => !bookedTimes.Contains(slot)).ToList();
        }

        private async Task<bool> IsTimeSlotBookedByUserAsync(string customerId, DateOnly bookingDate, TimeOnly bookingTime, string? currentBookingId = null)
        {
            return await _dbContext.Bookings
                .AsNoTracking()
                .AnyAsync(b => b.CustomerID == customerId
                            && b.BookingDate == bookingDate
                            && b.BookingTime == bookingTime
                            && b.BookingStatus != "Cancelled"
                            && (currentBookingId == null || b.BookingID != currentBookingId));
        }

        private async Task LoadReservationsAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                Reservations = [];
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var query = _dbContext.Bookings
                .AsNoTracking()
                .Where(booking => booking.CustomerID == customer.CustomerID)
                .AsQueryable();

            if (Category == "Upcoming")
            {
                query = query.Where(booking => booking.BookingDate >= today
                                            && booking.BookingStatus != "Completed"
                                            && booking.BookingStatus != "Cancelled");
            }
            else
            {
                query = query.Where(booking => booking.BookingStatus == "Completed"
                                            || booking.BookingStatus == "Cancelled"
                                            || booking.BookingDate < today);
            }

            if (DateFilter.HasValue)
            {
                query = query.Where(booking => booking.BookingDate == DateFilter.Value);
            }

            Reservations = await query
                .OrderBy(booking => booking.BookingDate)
                .ThenBy(booking => booking.BookingTime)
                .Select(booking => new ReservationItem(
                    booking.BookingID,
                    booking.BookingDate,
                    booking.BookingTime,
                    booking.PartySize,
                    booking.TableID,
                    booking.BookingStatus,
                    booking.BookingNotes,
                    booking.BookingStatus == "Waiting",
                    (booking.BookingStatus == "Waiting" || booking.BookingStatus == "Confirmed") && booking.BookingDate > today))
                .ToListAsync();
        }

        private bool ValidateReservationInput(DateOnly reservationDate, TimeOnly reservationTime, out string message)
        {
            var earliestDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
            if (reservationDate < earliestDate)
            {
                message = "Reservations can only be made for tomorrow or later.";
                return false;
            }

            if (!AllReservationSlots.Contains(reservationTime))
            {
                message = "Please select a valid reservation time between 9:00 AM and 5:00 PM.";
                return false;
            }

            if (ReservationForm.PartySize < 1)
            {
                message = "The number of people must be at least 1.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private async Task<CustomerEntity?> GetCurrentCustomerAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await _dbContext.Customers
                .Include(customer => customer.User)
                .SingleOrDefaultAsync(customer => customer.UserID == userId);
        }

        private async Task<string> GenerateNextBookingIdAsync()
        {
            var existingIds = await _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.BookingID != null && b.BookingID.StartsWith("RES-"))
                .Select(b => b.BookingID!)
                .ToListAsync();

            int maxNumber = 0;

            foreach (var id in existingIds)
            {
                if (id.Length > 4 && int.TryParse(id.Substring(4), out int parsedNumber))
                {
                    if (parsedNumber > maxNumber)
                    {
                        maxNumber = parsedNumber;
                    }
                }
            }

            int nextNumber = maxNumber + 1;
            return $"RES-{nextNumber:D3}";
        }

        public static bool IsValidCategory(string? category) =>
            string.Equals(category, "Upcoming", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Past", StringComparison.OrdinalIgnoreCase);

        private static IReadOnlyList<TimeOnly> BuildTimeSlots()
        {
            var slots = new List<TimeOnly>();
            var opening = new TimeOnly(9, 0);
            var closing = new TimeOnly(18, 0);

            for (var current = opening; current < closing; current = current.AddHours(1))
            {
                slots.Add(current);
            }

            return slots;
        }

        public sealed class ReservationFormInput
        {
            [Required(ErrorMessage = "Select a reservation date.")]
            public DateOnly? BookingDate { get; set; }

            [Required(ErrorMessage = "Select a reservation time.")]
            public TimeOnly? BookingTime { get; set; }

            [Range(1, 50, ErrorMessage = "The number of people must be at least 1.")]
            public int PartySize { get; set; } = 2;

            [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
            public string? BookingNotes { get; set; }
        }

        public sealed record ReservationItem(
            string BookingID,
            DateOnly BookingDate,
            TimeOnly BookingTime,
            int PartySize,
            string? TableID,
            string BookingStatus,
            string BookingNotes,
            bool CanModify,
            bool CanCancel);
    }
}
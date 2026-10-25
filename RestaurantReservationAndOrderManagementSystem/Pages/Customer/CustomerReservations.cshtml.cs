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
        private static readonly IReadOnlyList<TimeOnly> ReservationSlots = BuildTimeSlots();
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

        public IReadOnlyList<TimeOnly> TimeSlots => ReservationSlots;

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
            return Page();
        }

        private async Task<string> GenerateNextBookingIdAsync()
        {
            // Fetch all existing BookingIDs starting with 'RES-'
            var existingIds = await _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.BookingID != null && b.BookingID.StartsWith("RES-"))
                .Select(b => b.BookingID!)
                .ToListAsync();

            int maxNumber = 0;

            foreach (var id in existingIds)
            {
                // Extract the numeric portion after "RES-"
                if (id.Length > 4 && int.TryParse(id.Substring(4), out int parsedNumber))
                {
                    if (parsedNumber > maxNumber)
                    {
                        maxNumber = parsedNumber;
                    }
                }
            }

            // Increment and format to "RES-" followed by at least 3 digits (e.g., RES-001, RES-002, RES-010, RES-100)
            int nextNumber = maxNumber + 1;
            return $"RES-{nextNumber:D3}";
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
                return Page();
            }

            var result = await SaveReservationAsync(customer, isUpdate: false);
            if (result is not null)
            {
                return result;
            }

            await LoadReservationsAsync();
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
                return Page();
            }

            var booking = await _dbContext.Bookings
                .SingleOrDefaultAsync(item => item.BookingID == BookingId && item.CustomerID == customer.CustomerID);

            if (booking is null)
            {
                ReservationError = "That reservation could not be found.";
                await LoadReservationsAsync();
                return Page();
            }

            if (!string.Equals(booking.BookingStatus, "Waiting", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Only waiting reservations can be modified.");
                await LoadReservationsAsync();
                return Page();
            }

            var reservationDate = ReservationForm.BookingDate!.Value;
            var reservationTime = ReservationForm.BookingTime!.Value;

            if (!ValidateReservationInput(reservationDate, reservationTime, out var validationMessage))
            {
                ModelState.AddModelError(string.Empty, validationMessage);
                await LoadReservationsAsync();
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
                return Page();
            }

            var booking = await _dbContext.Bookings
                .SingleOrDefaultAsync(item => item.BookingID == BookingId && item.CustomerID == customer.CustomerID);

            if (booking is null)
            {
                ReservationError = "That reservation could not be found.";
                await LoadReservationsAsync();
                return Page();
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var canCancel = (booking.BookingStatus == "Waiting" || booking.BookingStatus == "Confirmed") &&
                booking.BookingDate > today;

            if (!canCancel)
            {
                ReservationError = "This reservation can no longer be cancelled.";
                await LoadReservationsAsync();
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
                return Page();
            }

            ReservationSuccess = "Your reservation has been submitted.";
            return RedirectToPage(new { category = Category, dateFilter = DateFilter?.ToString("yyyy-MM-dd") });
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
                // Exclude Cancelled and Completed statuses, and ensure date is today or in the future
                query = query.Where(booking => booking.BookingDate >= today
                                            && booking.BookingStatus != "Completed"
                                            && booking.BookingStatus != "Cancelled");
            }
            else
            {
                // Include Cancelled, Completed, or any reservation prior to today
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

            if (!ReservationSlots.Contains(reservationTime))
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

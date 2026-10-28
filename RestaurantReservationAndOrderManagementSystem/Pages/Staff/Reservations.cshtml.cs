using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class ReservationsModel : PageModel
    {
        private static readonly string[] BookingStatuses =
        [
            "Pending",
            "Confirmed",
            "Arriving",
            "On dine-in",
            "Completed",
            "Cancelled"
        ];

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ReservationsModel> _logger;

        public ReservationsModel(
            ApplicationDbContext dbContext,
            ILogger<ReservationsModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty(Name = "selectedDate", SupportsGet = true)]
        public DateOnly? SelectedDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty]
        public CreateReservationInput NewReservation { get; set; } = new();

        public IReadOnlyList<ReservationRow> Reservations { get; private set; } = [];
        public IReadOnlyList<TableOption> Tables { get; private set; } = [];
        public IReadOnlyList<string> Statuses => BookingStatuses;
        public int BookingsToday { get; private set; }
        public int GuestsToday { get; private set; }
        public int AvailableTables { get; private set; }
        public bool ShowCreateModal { get; private set; }

        public async Task OnGetAsync()
        {
            SelectedDate ??= DateOnly.FromDateTime(DateTime.Today);
            NewReservation.BookingDate ??= DateOnly.FromDateTime(DateTime.Today);
            NewReservation.BookingTime ??= TimeOnly.FromDateTime(DateTime.Now).AddMinutes(30);
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            ShowCreateModal = true;
            RemoveModelStateFor(nameof(SelectedDate));
            RemoveModelStateFor(nameof(Search));

            if (!ModelState.IsValid)
            {
                await LoadPageDataAsync();
                return Page();
            }

            var table = await _dbContext.Tables
                .SingleOrDefaultAsync(item => item.TableID == NewReservation.TableID);

            if (table is null)
            {
                ModelState.AddModelError("NewReservation.TableID", "Select a valid table.");
            }
            else if (table.MaxPax < NewReservation.PartySize)
            {
                ModelState.AddModelError(
                    "NewReservation.PartySize",
                    $"This table seats up to {table.MaxPax} guests.");
            }

            if (ModelState.ErrorCount > 0)
            {
                await LoadPageDataAsync();
                return Page();
            }

            var booking = new Booking
            {
                BookingID = Guid.NewGuid().ToString("N"),
                CustomerID = null,
                TableID = table!.TableID,
                BookingDate = NewReservation.BookingDate!.Value,
                BookingTime = NewReservation.BookingTime!.Value,
                PartySize = NewReservation.PartySize,
                BookingStatus = "Confirmed",
                BookingNotes = NewReservation.BookingNotes?.Trim() ?? string.Empty
            };

            _dbContext.Bookings.Add(booking);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not save the new reservation.");
                ModelState.AddModelError(string.Empty, "The reservation could not be saved. Please try again.");
                await LoadPageDataAsync();
                return Page();
            }

            TempData["SuccessMessage"] = "Reservation created successfully.";
            return RedirectToPage(new
            {
                selectedDate = NewReservation.BookingDate.Value.ToString("yyyy-MM-dd"),
                search = Search
            });
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(
            string? bookingId,
            string? newStatus,
            string? tableId)
        {
            if (string.IsNullOrWhiteSpace(bookingId) ||
                string.IsNullOrWhiteSpace(newStatus) ||
                !BookingStatuses.Contains(newStatus, StringComparer.Ordinal))
            {
                return BadRequest();
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var booking = await _dbContext.Bookings
                .SingleOrDefaultAsync(item => item.BookingID == bookingId);
            if (booking is null)
            {
                return NotFound();
            }

            try
            {
                if ((booking.BookingStatus is "Pending" or "Waiting") && newStatus == "Confirmed")
                {
                    if (string.IsNullOrWhiteSpace(tableId))
                    {
                        TempData["ReservationStatusError"] =
                            "Select an available table before confirming this reservation.";
                        return RedirectToPage(new
                        {
                            selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                            search = Search
                        });
                    }

                    var table = await _dbContext.Tables
                        .SingleOrDefaultAsync(item => item.TableID == tableId);
                    if (table is null ||
                        !table.TableAvailability.Equals("Available", StringComparison.OrdinalIgnoreCase) ||
                        table.MaxPax < booking.PartySize)
                    {
                        TempData["ReservationStatusError"] = table is null
                            ? "The selected table does not exist."
                            : !table.TableAvailability.Equals("Available", StringComparison.OrdinalIgnoreCase)
                                ? "The selected table is not available."
                                : $"The selected table seats up to {table.MaxPax} guests.";
                        return RedirectToPage(new
                        {
                            selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                            search = Search
                        });
                    }

                    var tableAlreadyBooked = await _dbContext.Bookings.AnyAsync(item =>
                        item.BookingID != booking.BookingID &&
                        item.TableID == table.TableID &&
                        item.BookingDate == booking.BookingDate &&
                        item.BookingTime == booking.BookingTime &&
                        item.BookingStatus != "Cancelled" &&
                        item.BookingStatus != "Completed");
                    if (tableAlreadyBooked)
                    {
                        TempData["ReservationStatusError"] =
                            "The selected table already has an active reservation at that date and time.";
                        return RedirectToPage(new
                        {
                            selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                            search = Search
                        });
                    }

                    booking.TableID = table.TableID;
                }

                await ReservationStatusWorkflow.ApplyAsync(_dbContext, booking, newStatus);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Could not update reservation status for booking {BookingId}.",
                    bookingId);
                TempData["ReservationStatusError"] = "The reservation status could not be updated. Please try again.";
                return RedirectToPage(new { selectedDate = SelectedDate?.ToString("yyyy-MM-dd"), search = Search });
            }

            TempData["ReservationStatusMessage"] = $"Reservation status changed to {newStatus}.";
            return RedirectToPage(new { selectedDate = SelectedDate?.ToString("yyyy-MM-dd"), search = Search });
        }

        public async Task<IActionResult> OnPostReassignTableAsync(string? bookingId, string? tableId)
        {
            if (string.IsNullOrWhiteSpace(bookingId) || string.IsNullOrWhiteSpace(tableId))
            {
                return BadRequest();
            }

            var booking = await _dbContext.Bookings
                .SingleOrDefaultAsync(item => item.BookingID == bookingId);
            if (booking is null)
            {
                return NotFound();
            }

            if (booking.TableID == tableId)
            {
                return RedirectToPage(new
                {
                    selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                    search = Search
                });
            }

            var table = await _dbContext.Tables
                .SingleOrDefaultAsync(item => item.TableID == tableId);
            if (table is null ||
                !table.TableAvailability.Equals("Available", StringComparison.OrdinalIgnoreCase) ||
                table.MaxPax < booking.PartySize)
            {
                TempData["ReservationTableError"] =
                    table is null
                        ? "The selected table does not exist."
                        : !table.TableAvailability.Equals("Available", StringComparison.OrdinalIgnoreCase)
                            ? "The selected table is not available."
                            : $"The selected table seats up to {table.MaxPax} guests.";
                return RedirectToPage(new
                {
                    selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                    search = Search
                });
            }

            var tableAlreadyBooked = await _dbContext.Bookings.AnyAsync(item =>
                item.BookingID != booking.BookingID &&
                item.TableID == tableId &&
                item.BookingDate == booking.BookingDate &&
                item.BookingTime == booking.BookingTime &&
                item.BookingStatus != "Cancelled" &&
                item.BookingStatus != "Completed");
            if (tableAlreadyBooked)
            {
                TempData["ReservationTableError"] =
                    "The selected table already has an active reservation at that date and time.";
                return RedirectToPage(new
                {
                    selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                    search = Search
                });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var previousTable = await _dbContext.Tables
                    .SingleOrDefaultAsync(item => item.TableID == booking.TableID);
                if (previousTable is not null &&
                    (previousTable.TableAvailability.Equals("Reserved", StringComparison.OrdinalIgnoreCase) ||
                     previousTable.TableAvailability.Equals("On dine-in", StringComparison.OrdinalIgnoreCase) ||
                     previousTable.TableAvailability.Equals("Occupied", StringComparison.OrdinalIgnoreCase)))
                {
                    previousTable.TableAvailability = "Available";
                }

                booking.TableID = table.TableID;
                if (booking.BookingStatus is "Arriving" or "On dine-in")
                {
                    await ReservationStatusWorkflow.ApplyAsync(
                        _dbContext,
                        booking,
                        booking.BookingStatus);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(
                    exception,
                    "Could not reassign table for booking {BookingId}.",
                    booking.BookingID);
                TempData["ReservationTableError"] = "The table could not be reassigned. Please try again.";
                return RedirectToPage(new
                {
                    selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                    search = Search
                });
            }

            TempData["ReservationTableMessage"] = "Reservation table reassigned.";
            return RedirectToPage(new
            {
                selectedDate = SelectedDate?.ToString("yyyy-MM-dd"),
                search = Search
            });
        }

        public static string StatusCssClass(string status) => status switch
        {
            "Pending" or "Waiting" => "status-pending",
            "Confirmed" => "status-confirmed",
            "Arriving" => "status-arriving",
            "On dine-in" => "status-dining",
            "Completed" => "status-completed",
            "Cancelled" => "status-cancelled",
            _ => "status-waiting"
        };

        private async Task LoadPageDataAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            BookingsToday = await _dbContext.Bookings.CountAsync(booking => booking.BookingDate == today);
            GuestsToday = await _dbContext.Bookings
                .Where(booking => booking.BookingDate == today)
                .SumAsync(booking => (int?)booking.PartySize) ?? 0;
            AvailableTables = await _dbContext.Tables.CountAsync(table =>
                table.TableAvailability.ToLower() == "available");

            var bookingQuery = _dbContext.Bookings
                .AsNoTracking()
                .Include(booking => booking.Customer)
                    .ThenInclude(customer => customer!.User)
                .Include(booking => booking.Table)
                .AsQueryable();

            if (SelectedDate.HasValue)
            {
                bookingQuery = bookingQuery.Where(booking => booking.BookingDate == SelectedDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var searchTerm = Search.Trim();
                bookingQuery = bookingQuery.Where(booking =>
                    (booking.Customer != null &&
                     (booking.Customer.User.FullName.Contains(searchTerm) ||
                      booking.Customer.User.Phone.Contains(searchTerm))) ||
                    (booking.Customer == null && "Walk-in".Contains(searchTerm)) ||
                    (booking.Table != null && booking.Table.TableID.Contains(searchTerm)));
            }

            Reservations = await bookingQuery
                .OrderBy(booking => booking.BookingDate)
                .ThenBy(booking => booking.BookingTime)
                .Select(booking => new ReservationRow(
                    booking.BookingID,
                    booking.BookingDate,
                    booking.BookingTime,
                    booking.Customer == null ? "Walk-in" : booking.Customer.User.FullName,
                    booking.Customer == null ? string.Empty : booking.Customer.User.Phone,
                    booking.PartySize,
                    booking.Table!.TableID,
                    booking.BookingStatus))
                .ToListAsync();

            Tables = await _dbContext.Tables
                .AsNoTracking()
                .OrderBy(table => table.TableID)
                .Select(table => new TableOption(
                    table.TableID,
                    table.MaxPax,
                    table.TableAvailability))
                .ToListAsync();
        }

        private void RemoveModelStateFor(string prefix)
        {
            foreach (var key in ModelState.Keys
                         .Where(key => key.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                       key.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                ModelState.Remove(key);
            }
        }

        public sealed class CreateReservationInput
        {
            [Required(ErrorMessage = "Select a table.")]
            public string TableID { get; set; } = string.Empty;

            [Required(ErrorMessage = "Reservation date is required.")]
            public DateOnly? BookingDate { get; set; }

            [Required(ErrorMessage = "Reservation time is required.")]
            public TimeOnly? BookingTime { get; set; }

            [Range(1, 100, ErrorMessage = "Party size must be at least 1.")]
            public int PartySize { get; set; } = 2;

            [StringLength(500)]
            public string? BookingNotes { get; set; }
        }

        public sealed record ReservationRow(
            string BookingID,
            DateOnly BookingDate,
            TimeOnly BookingTime,
            string GuestName,
            string GuestPhone,
            int PartySize,
            string TableID,
            string Status);

        public sealed record TableOption(string TableID, int Capacity, string Availability);
    }
}

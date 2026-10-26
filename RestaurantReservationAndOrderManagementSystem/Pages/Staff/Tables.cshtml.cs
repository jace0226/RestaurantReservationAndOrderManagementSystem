using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class TablesModel : PageModel
    {
        private static readonly string[] AvailabilityStatusesList =
        [
            "Available",
            "Reserved",
            "On dine-in"
        ];

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<TablesModel> _logger;

        public TablesModel(ApplicationDbContext dbContext, ILogger<TablesModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = string.Empty;

        public UpdateTableInput TableUpdate { get; private set; } = new();

        public IReadOnlyList<TableCard> Tables { get; private set; } = [];
        public IReadOnlyList<string> AvailabilityStatuses => AvailabilityStatusesList;
        public int TotalTables { get; private set; }
        public int AvailableTables { get; private set; }
        public int ReservedTables { get; private set; }
        public int OccupiedTables { get; private set; }
        public async Task OnGetAsync()
        {
            await LoadTablesAsync();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            string? tableId,
            int capacity,
            string? availability)
        {
            TableUpdate = new UpdateTableInput
            {
                TableID = tableId ?? string.Empty,
                Capacity = capacity,
                Availability = availability ?? string.Empty
            };
            ModelState.Clear();

            if (!TryValidateModel(TableUpdate, nameof(TableUpdate)))
            {
                await LoadTablesAsync();
                return Page();
            }

            if (!AvailabilityStatusesList.Contains(TableUpdate.Availability, StringComparer.Ordinal))
            {
                ModelState.AddModelError("TableUpdate.Availability", "Select a valid table status.");
                await LoadTablesAsync();
                return Page();
            }

            var table = await _dbContext.Tables
                .SingleOrDefaultAsync(item => item.TableID == TableUpdate.TableID);
            if (table is null)
            {
                return NotFound();
            }

            var largestActiveParty = await _dbContext.Bookings
                .Where(booking =>
                    booking.TableID == table.TableID &&
                    booking.BookingStatus != "Cancelled" &&
                    booking.BookingStatus != "Completed")
                .Select(booking => (int?)booking.PartySize)
                .MaxAsync();
            if (largestActiveParty.HasValue && largestActiveParty.Value > TableUpdate.Capacity)
            {
                ModelState.AddModelError(
                    "TableUpdate.Capacity",
                    $"This table has an active reservation for {largestActiveParty.Value} guests.");
                await LoadTablesAsync();
                return Page();
            }

            table.MaxPax = TableUpdate.Capacity;
            table.TableAvailability = TableUpdate.Availability;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not update table {TableId}.", table.TableID);
                ModelState.AddModelError(string.Empty, "The table could not be updated. Please try again.");
                await LoadTablesAsync();
                return Page();
            }

            return RedirectToPage(new { statusFilter = StatusFilter, search = Search });
        }

        public async Task<IActionResult> OnPostDeleteAsync(string tableId)
        {
            if (!User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(tableId))
            {
                return BadRequest();
            }

            var table = await _dbContext.Tables
                .FirstOrDefaultAsync(t => t.TableID == tableId);

            if (table == null)
            {
                return NotFound();
            }

            _dbContext.Tables.Remove(table);

            await _dbContext.SaveChangesAsync();

            return RedirectToPage(new
            {
                StatusFilter,
                Search
            });
        }

        public static string StatusCssClass(string status) => status.ToLowerInvariant() switch
        {
            "available" => "table-available",
            "reserved" => "table-reserved",
            "on dine-in" or "occupied" => "table-occupied",
            _ => "table-available"
        };

        private async Task LoadTablesAsync()
        {
            var query = _dbContext.Tables.AsNoTracking();
            TotalTables = await _dbContext.Tables.CountAsync();
            AvailableTables = await CountStatusAsync("Available");
            ReservedTables = await CountStatusAsync("Reserved");
            OccupiedTables = await _dbContext.Tables.CountAsync(table =>
                table.TableAvailability.ToLower() == "on dine-in" ||
                table.TableAvailability.ToLower() == "occupied");

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(table => table.TableID.Contains(term));
            }

            if (AvailabilityStatusesList.Contains(StatusFilter, StringComparer.Ordinal))
            {
                var filter = StatusFilter.ToLowerInvariant();
                if (filter == "on dine-in")
                {
                    query = query.Where(table =>
                        table.TableAvailability.ToLower() == "on dine-in" ||
                        table.TableAvailability.ToLower() == "occupied");
                }
                else
                {
                    query = query.Where(table => table.TableAvailability.ToLower() == filter);
                }
            }
            else
            {
                StatusFilter = "All";
            }

            Tables = await query
                .OrderBy(table => table.TableID)
                .Select(table => new TableCard(
                    table.TableID,
                    table.MaxPax,
                    table.TableAvailability))
                .ToListAsync();
        }

        private Task<int> CountStatusAsync(string status) =>
            _dbContext.Tables.CountAsync(table =>
                table.TableAvailability.ToLower() == status.ToLower());

        public sealed class UpdateTableInput
        {
            [Required]
            public string TableID { get; set; } = string.Empty;

            [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
            public int Capacity { get; set; }

            [Required]
            public string Availability { get; set; } = string.Empty;
        }

        public sealed class DeleteTableInput
        {
            [Required]
            public string TableID { get; set; } = string.Empty;

            [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
            public int Capacity { get; set; }

            [Required]
            public string Availability { get; set; } = string.Empty;
        }

        public sealed record TableCard(string TableID, int Capacity, string Availability);
    }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class MenusModel : PageModel
    {
        private static readonly string[] AvailabilityStatuses = ["Available", "Unavailable"];

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<MenusModel> _logger;

        public MenusModel(ApplicationDbContext dbContext, ILogger<MenusModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Search { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string AvailabilityFilter { get; set; } = "All";

        [BindProperty]
        public CreateMenuItemInput NewMenuItem { get; set; } = new();

        public IReadOnlyList<MenuItemRow> MenuItems { get; private set; } = [];
        public int TodayOrderCount { get; private set; }
        public int AvailableItemCount { get; private set; }
        public int TodayReservationCount { get; private set; }
        public bool ShowCreateForm { get; private set; }

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            ShowCreateForm = true;
            RemoveSearchModelState();
            if (!TryValidateModel(NewMenuItem, nameof(NewMenuItem)))
            {
                await LoadPageDataAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var staffId = await _dbContext.Staffs
                .Where(staff => staff.UserID == userId)
                .Select(staff => staff.StaffID)
                .SingleOrDefaultAsync();
            if (staffId is null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your staff account is not linked to a staff record, so menu items cannot be added.");
                await LoadPageDataAsync();
                return Page();
            }

            var menuItem = new Food_Items
            {
                FoodID = Guid.NewGuid().ToString("N"),
                StaffID = staffId,
                FoodName = NewMenuItem.Name.Trim(),
                Price = NewMenuItem.Price,
                FoodDescription = NewMenuItem.Description?.Trim() ?? string.Empty,
                FoodAvailability = "Available"
            };

            _dbContext.FoodItems.Add(menuItem);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not add menu item {FoodName}.", menuItem.FoodName);
                ModelState.AddModelError(string.Empty, "The menu item could not be saved. Please try again.");
                await LoadPageDataAsync();
                return Page();
            }

            return RedirectToPage(new { search = Search, availabilityFilter = AvailabilityFilter });
        }

        public async Task<IActionResult> OnPostUpdateAvailabilityAsync(
            string? foodId,
            string? availability)
        {
            if (string.IsNullOrWhiteSpace(foodId) ||
                availability is null ||
                !AvailabilityStatuses.Contains(availability, StringComparer.Ordinal))
            {
                return BadRequest();
            }

            var menuItem = await _dbContext.FoodItems
                .SingleOrDefaultAsync(item => item.FoodID == foodId);
            if (menuItem is null)
            {
                return NotFound();
            }

            menuItem.FoodAvailability = availability;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not update availability for menu item {FoodId}.", foodId);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return RedirectToPage(new { search = Search, availabilityFilter = AvailabilityFilter });
        }

        private async Task LoadPageDataAsync()
        {
            if (AvailabilityFilter is not ("All" or "Available" or "Unavailable"))
            {
                AvailabilityFilter = "All";
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            TodayOrderCount = await _dbContext.Orders.CountAsync(order =>
                order.CreatedAt >= today && order.CreatedAt < tomorrow);
            TodayReservationCount = await _dbContext.Bookings.CountAsync(booking =>
                booking.BookingDate == DateOnly.FromDateTime(today) &&
                booking.BookingStatus != "Cancelled");

            var items = await _dbContext.FoodItems
                .AsNoTracking()
                .OrderBy(item => item.FoodName)
                .ToListAsync();
            AvailableItemCount = items.Count(item =>
                item.FoodAvailability.Equals("Available", StringComparison.OrdinalIgnoreCase));

            var query = items.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                query = query.Where(item =>
                    item.FoodName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    item.FoodDescription.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            MenuItems = query.Select(item => new MenuItemRow(
                item.FoodID,
                item.FoodName,
                item.Price,
                item.FoodDescription,
                item.FoodAvailability)).ToList();
        }

        private void RemoveSearchModelState()
        {
            ModelState.Remove(nameof(Search));
            ModelState.Remove(nameof(AvailabilityFilter));
        }

        public sealed class CreateMenuItemInput
        {
            [Required(ErrorMessage = "Enter a menu item name.")]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "Enter a price greater than zero.")]
            public decimal Price { get; set; }

            [StringLength(1000)]
            public string? Description { get; set; }
        }

        public sealed record MenuItemRow(
            string FoodId,
            string Name,
            decimal Price,
            string Description,
            string Availability);
    }
}

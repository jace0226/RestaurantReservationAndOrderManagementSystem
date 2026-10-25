using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    public class CustomerMenusModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CustomerMenusModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Direct Entity Lists
        public List<Food_Items> MenuItems { get; set; } = new();
        public List<Table> AvailableTables { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            string currentCustomerId = HttpContext.Session.GetString("CustomerID") ?? "1001";

            // 1. Fetch all food items directly from DB
            MenuItems = await _context.FoodItems.ToListAsync();

            // 2. Compute dynamic recommendations
            bool hasAnyOrderData = await _context.OrderItems.AnyAsync();
            List<string> recommendedFoodIds = new();
            string dynamicLabel = string.Empty;

            if (!hasAnyOrderData)
            {
                recommendedFoodIds = MenuItems
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .Select(f => f.FoodID)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList()!;

                dynamicLabel = "Chef's Pick";
            }
            else
            {
                bool hasUserOrders = await _context.Orders.AnyAsync(o => o.CustomerID == currentCustomerId);

                if (hasUserOrders)
                {
                    string topFoodId = await _context.OrderItems
                        .Where(oi => oi.Order.CustomerID == currentCustomerId)
                        .GroupBy(oi => oi.FoodID)
                        .OrderByDescending(g => g.Sum(oi => oi.Quantity))
                        .Select(g => g.Key)
                        .FirstOrDefaultAsync() ?? string.Empty;

                    if (!string.IsNullOrEmpty(topFoodId))
                    {
                        recommendedFoodIds.Add(topFoodId);
                    }

                    dynamicLabel = "Your Favorite";
                }
                else
                {
                    string topFoodId = await _context.OrderItems
                        .GroupBy(oi => oi.FoodID)
                        .OrderByDescending(g => g.Sum(oi => oi.Quantity))
                        .Select(g => g.Key)
                        .FirstOrDefaultAsync() ?? string.Empty;

                    if (!string.IsNullOrEmpty(topFoodId))
                    {
                        recommendedFoodIds.Add(topFoodId);
                    }

                    dynamicLabel = "Popular Choice";
                }
            }

            // 3. Assign the dynamic label to the [NotMapped] Recommendation property
            foreach (var item in MenuItems)
            {
                if (!string.IsNullOrEmpty(item.FoodID) && recommendedFoodIds.Contains(item.FoodID))
                {
                    item.Recommendation = dynamicLabel;
                }
            }

            // 4. Load available tables
            AvailableTables = await _context.Tables
                .Where(t => t.TableAvailability == "On dine-in")
                .ToListAsync();

            return Page();
        }
    }
}
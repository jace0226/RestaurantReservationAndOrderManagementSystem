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

        // ViewModel to pass items and dynamic recommendation tags to JS
        public class MenuItemDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string FullDescription { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string Image { get; set; } = string.Empty;
            public string ImageAlt { get; set; } = string.Empty;
            public bool Available { get; set; }
            public string PrepTime { get; set; } = string.Empty;
            public string Dietary { get; set; } = string.Empty;
            public string Recommendation { get; set; } = string.Empty;
        }

        public List<MenuItemDto> MenuItems { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // Retrieve CustomerID from session; fallback to temporary Customer ID "1001"
            string currentCustomerId = HttpContext.Session.GetString("CustomerID") ?? "1001";

            // 1. Fetch all food items from the database
            var dbFoodItems = await _context.FoodItems.ToListAsync();

            // 2. Check if there are any order items in the database at all
            bool hasAnyOrderData = await _context.OrderItems.AnyAsync();

            List<string> recommendedFoodIds = new();
            string dynamicLabel = string.Empty;

            if (!hasAnyOrderData)
            {
                // FALLBACK: No order history exists yet -> pick 3 random dishes as "Chef's Pick"
                recommendedFoodIds = dbFoodItems
                    .OrderBy(_ => Guid.NewGuid()) // Randomize list
                    .Take(3)
                    .Select(f => f.FoodID)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList()!;

                dynamicLabel = "Chef's Pick";
            }
            else
            {
                // Check if current user has placed any orders
                bool hasUserOrders = await _context.Orders.AnyAsync(o => o.CustomerID == currentCustomerId);

                if (hasUserOrders)
                {
                    // USER HAS ORDERS: Find their most frequently ordered FoodID
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
                    // GUEST / NO USER ORDERS: Find the most ordered FoodID overall across all customers
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

            // 3. Map database models to DTOs and assign dynamic recommendation label
            MenuItems = dbFoodItems.Select(item => new MenuItemDto
            {
                Id = item.FoodID ?? string.Empty,
                Name = item.FoodName ?? "Unnamed Dish",
                Category = item.Category ?? "Main Course",
                Description = item.FoodDescription ?? string.Empty,
                FullDescription = item.FullDescription ?? item.FoodDescription ?? string.Empty,
                Price = item.Price,
                Image = !string.IsNullOrEmpty(item.ImageUrl) ? item.ImageUrl : "/images/placeholder-food.jpg",
                ImageAlt = item.ImageAlt ?? item.FoodName ?? "Food image",
                Available = string.Equals(item.FoodAvailability, "Available", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(item.FoodAvailability, "True", StringComparison.OrdinalIgnoreCase),
                PrepTime = item.PrepTime ?? "15-20 mins",
                Dietary = item.Dietary ?? "Standard",
                Recommendation = (!string.IsNullOrEmpty(item.FoodID) && recommendedFoodIds.Contains(item.FoodID)) 
                                 ? dynamicLabel 
                                 : string.Empty
            }).ToList();

            return Page();
        }
    }
}
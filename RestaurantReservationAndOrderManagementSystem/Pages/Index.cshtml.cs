using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantReservationAndOrderManagementSystem.Data;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;

        public IndexModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IReadOnlyList<FeaturedMenuItem> FeaturedItems { get; private set; } = [];

        public async Task OnGetAsync()
        {
            FeaturedItems = await _dbContext.FoodItems
                .AsNoTracking()
                .Where(item => item.FoodAvailability.ToLower() == "available")
                .OrderBy(item => item.FoodName)
                .Take(3)
                .Select(item => new FeaturedMenuItem(
                    item.FoodName,
                    item.FoodDescription,
                    item.Price))
                .ToListAsync();
        }

        public sealed record FeaturedMenuItem(string Name, string Description, decimal Price);
    }
}

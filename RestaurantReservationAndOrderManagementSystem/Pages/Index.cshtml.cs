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
        public IReadOnlyList<LandingReview> RecentReviews { get; private set; } = [];
        public double AverageReviewRating { get; private set; }
        public int ReviewCount { get; private set; }

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
                    item.Price,
                    item.ImageUrl,
                    item.ImageAlt))
                .ToListAsync();

            ReviewCount = await _dbContext.Feedbacks.CountAsync();
            AverageReviewRating = ReviewCount == 0
                ? 0
                : await _dbContext.Feedbacks.AverageAsync(feedback => (double)feedback.Stars);

            RecentReviews = await _dbContext.Feedbacks
                .AsNoTracking()
                .Include(feedback => feedback.Customer)
                .ThenInclude(customer => customer.User)
                .Where(feedback => !string.IsNullOrWhiteSpace(feedback.Message))
                .OrderByDescending(feedback => feedback.FeedbackID)
                .Take(3)
                .Select(feedback => new LandingReview(
                    feedback.Customer.User.FullName,
                    feedback.Stars,
                    feedback.Message))
                .ToListAsync();
        }

        public sealed record FeaturedMenuItem(
            string Name,
            string Description,
            decimal Price,
            string ImageUrl,
            string ImageAlt);

        public sealed record LandingReview(
            string CustomerName,
            int Stars,
            string Message);
    }
}

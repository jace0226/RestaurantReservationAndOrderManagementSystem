using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Staff
{
    public class ReviewManagementModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ReviewManagementModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public class ReviewViewModel {
            public string FullName { get; set; } = string.Empty;
            public int Stars { get; set; } = 0;
            public string Message { get; set; } = string.Empty;
        }

        public List<ReviewViewModel> Reviews { get; set; } = new();

        [BindProperty]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public string? FilterRating { get; set; }

        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int PositiveReviews { get; set; }
        public int NegativeReviews { get; set; }

        public async Task OnGetAsync() {
            await LoadReviewsAsync();
        }

        private async Task LoadReviewsAsync() {
            // Total number of reviews
            TotalReviews = await _context.Feedbacks.CountAsync();

            // Average star rating
            AverageRating = TotalReviews > 0
                ? await _context.Feedbacks.AverageAsync(f => (double)f.Stars)
                : 0;

            // Positive reviews: 4 or 5 stars
            PositiveReviews = await _context.Feedbacks
                .CountAsync(f => f.Stars >= 4 && f.Stars <= 5);

            // Negative reviews: 1 or 2 stars
            NegativeReviews = await _context.Feedbacks
                .CountAsync(f => f.Stars >= 1 && f.Stars <= 2);

            var query =
                from feedback in _context.Feedbacks
                join customer in _context.Customers
                    on feedback.CustomerID equals customer.CustomerID
                join user in _context.Users
                    on customer.UserID equals user.UserID
                select new ReviewViewModel
                {
                    FullName = user.FullName,
                    Stars = feedback.Stars,
                    Message = feedback.Message
                };

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                string term = SearchTerm.Trim();

                query = query.Where(r =>
                    r.FullName.Contains(term) ||
                    r.Message.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(FilterRating))
            {

                if (int.TryParse(FilterRating, out int rating))
                {
                    query = query.Where(r => r.Stars == rating);
                }
            }

            Reviews = await query.ToListAsync();
        }
    }
}

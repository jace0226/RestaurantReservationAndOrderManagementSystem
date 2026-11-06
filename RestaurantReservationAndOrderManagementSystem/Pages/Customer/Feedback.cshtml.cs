using System.Text.RegularExpressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    [Authorize(Roles = "Customer")]
    public class FeedbackModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<FeedbackModel> _logger;

        public FeedbackModel(
            ApplicationDbContext dbContext,
            ILogger<FeedbackModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty]
        public int? Rating { get; set; }

        [BindProperty]
        public string? Message { get; set; }

        [TempData]
        public string? FeedbackSuccess { get; set; }

        [TempData]
        public string? FeedbackError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDashboardAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Message = Message?.Trim();
            ModelState.Remove(nameof(Message));

            if (!Rating.HasValue)
            {
                ModelState.AddModelError(nameof(Rating), "Please select a star rating.");
            }
            else if (Rating.Value is < 1 or > 5)
            {
                ModelState.AddModelError(nameof(Rating), "Select a rating from 1 to 5 stars.");
            }

            if (string.IsNullOrWhiteSpace(Message))
            {
                ModelState.AddModelError(nameof(Message), "Please enter your feedback message.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDashboardAsync();
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var customerId = await _dbContext.Customers
                .Where(customer => customer.UserID == userId)
                .Select(customer => customer.CustomerID)
                .SingleOrDefaultAsync();

            if (customerId is null)
            {
                return NotFound();
            }

            // Fetch existing FB- IDs into memory
            var existingIds = await _dbContext.Feedbacks
                .AsNoTracking()
                .Where(f => f.FeedbackID != null && f.FeedbackID.StartsWith("FB-"))
                .Select(f => f.FeedbackID!)
                .ToListAsync();

            // Extract numeric suffix in-memory to find max ID
            var maxId = existingIds
                .Select(id => Regex.Match(id, @"^FB-(\d+)$"))
                .Where(m => m.Success)
                .Select(m => int.Parse(m.Groups[1].Value))
                .DefaultIfEmpty(0)
                .Max();

            var newFeedbackId = $"FB-{(maxId + 1):D3}";

            var feedback = new Feedback
            {
                FeedbackID = newFeedbackId,
                CustomerID = customerId,
                Stars = Rating!.Value,
                Message = Message!
            };

            _dbContext.Feedbacks.Add(feedback);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not save feedback for customer {CustomerId}.", customerId);
                FeedbackError = "We could not submit your feedback. Please try again.";
                await LoadDashboardAsync();
                return Page();
            }

            FeedbackSuccess = "Thank you for sharing your feedback.";
            return RedirectToPage();
        }

        public int TotalRatings { get; private set; }

        public double AverageRating { get; private set; }

        public IReadOnlyList<RatingBreakdownItem> RatingBreakdown { get; private set; } = [];

        public IReadOnlyList<FeedbackListItem> RecentFeedbacks { get; private set; } = [];

        private async Task LoadDashboardAsync()
        {
            TotalRatings = await _dbContext.Feedbacks.CountAsync();
            AverageRating = TotalRatings == 0
                ? 0
                : await _dbContext.Feedbacks.AverageAsync(feedback => (double)feedback.Stars);

            var countsByRating = await _dbContext.Feedbacks
                .GroupBy(feedback => feedback.Stars)
                .Select(group => new { Stars = group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.Stars, group => group.Count);

            RatingBreakdown = Enumerable.Range(1, 5)
                .Reverse()
                .Select(stars =>
                {
                    var count = countsByRating.GetValueOrDefault(stars);
                    var label = stars switch
                    {
                        5 => "FIVE",
                        4 => "FOUR",
                        3 => "THREE",
                        2 => "TWO",
                        _ => "ONE"
                    };

                    return new RatingBreakdownItem(
                        stars,
                        label,
                        count,
                        TotalRatings == 0 ? 0 : count * 100d / TotalRatings,
                        FormatCount(count));
                })
                .ToArray();

            RecentFeedbacks = await _dbContext.Feedbacks
                .AsNoTracking()
                .OrderByDescending(feedback => feedback.FeedbackID)
                .Take(10)
                .Select(feedback => new FeedbackListItem(
                    feedback.Customer != null && feedback.Customer.User != null && !string.IsNullOrWhiteSpace(feedback.Customer.User.FullName)
                        ? feedback.Customer.User.FullName
                        : "Anonymous",
                    feedback.Stars,
                    feedback.Message))
                .ToListAsync();
        }

        private static string FormatCount(int count)
        {
            return count >= 1000
                ? (count / 1000d).ToString("0.#") + "K"
                : count.ToString("N0");
        }

        public sealed record RatingBreakdownItem(
            int Stars,
            string Label,
            int Count,
            double Percentage,
            string FormattedCount);

        public sealed record FeedbackListItem(
            string DisplayName,
            int Stars,
            string? Message);
    }
}
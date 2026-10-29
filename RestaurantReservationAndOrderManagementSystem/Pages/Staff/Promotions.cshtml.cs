using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Staff
{
    [Authorize(Roles = "Administrator,Staff")]
    public class PromotionsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly PromotionService _promotionService;
        private readonly ILogger<PromotionsModel> _logger;

        public PromotionsModel(
            ApplicationDbContext dbContext,
            PromotionService promotionService,
            ILogger<PromotionsModel> logger)
        {
            _dbContext = dbContext;
            _promotionService = promotionService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        public IReadOnlyList<PromotionRow> Promotions { get; private set; } = [];
        public IReadOnlyList<Food_Items> AvailableFoods { get; private set; } = [];

        public int TotalPromotionsCount { get; private set; }
        public int ActivePromotionsCount { get; private set; }
        public int UpcomingPromotionsCount { get; private set; }
        public int EndedPromotionsCount { get; private set; }

        [BindProperty]
        public PromoFormModel Input { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAddPromotionAsync()
        {
            if (!User.IsInRole("Administrator"))
            {
                TempData["ErrorMessage"] = "Only administrators are authorized to create promotions.";
                return RedirectToPage();
            }

            var food = await _dbContext.FoodItems.FirstOrDefaultAsync(f => f.FoodID == Input.FoodID);
            if (food == null)
            {
                TempData["ErrorMessage"] = "The selected food item does not exist.";
                return RedirectToPage();
            }

            if (Input.DiscountedPrice <= 0)
            {
                TempData["ErrorMessage"] = "Promotional price must be greater than RM 0.00.";
                return RedirectToPage();
            }

            if (Input.DiscountedPrice >= food.Price)
            {
                TempData["ErrorMessage"] = $"Promotional price (RM {Input.DiscountedPrice:N2}) must be lower than original price (RM {food.Price:N2}).";
                return RedirectToPage();
            }

            // Resolve Staff ID of current user
            var staffId = await GetCurrentStaffIdAsync();
            if (string.IsNullOrWhiteSpace(staffId))
            {
                staffId = await _dbContext.Staffs.Select(s => s.StaffID).FirstOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(staffId))
                {
                    TempData["ErrorMessage"] = "Cannot create promotion: No staff record found in the system.";
                    return RedirectToPage();
                }
            }

            var newPromoId = await GenerateNextPromoIdAsync();

            var startValidation = ValidatePromotionWindow();
            if (startValidation is not null) return startValidation;

            var promotion = new Promotion
            {
                PromoID = newPromoId,
                FoodID = Input.FoodID,
                StaffID = staffId,
                DiscountedPrice = Input.DiscountedPrice,
                StartDateTime = Input.StartDate.ToDateTime(Input.StartTime),
                EndDateTime = Input.EndDate.ToDateTime(Input.EndTime)
            };

            try
            {
                _dbContext.Promotions.Add(promotion);
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Promotion {newPromoId} for \"{food.FoodName}\" created successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create promotion.");
                TempData["ErrorMessage"] = "An error occurred while saving the promotion: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditPromotionAsync()
        {
            if (!User.IsInRole("Administrator"))
            {
                TempData["ErrorMessage"] = "Only administrators are authorized to edit promotions.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(Input.PromoID))
            {
                TempData["ErrorMessage"] = "Promotion ID is missing.";
                return RedirectToPage();
            }

            var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.PromoID == Input.PromoID);
            if (promotion == null)
            {
                TempData["ErrorMessage"] = "Promotion not found.";
                return RedirectToPage();
            }

            var food = await _dbContext.FoodItems.FirstOrDefaultAsync(f => f.FoodID == Input.FoodID);
            if (food == null)
            {
                TempData["ErrorMessage"] = "The selected food item does not exist.";
                return RedirectToPage();
            }

            if (Input.DiscountedPrice <= 0)
            {
                TempData["ErrorMessage"] = "Promotional price must be greater than RM 0.00.";
                return RedirectToPage();
            }

            if (Input.DiscountedPrice >= food.Price)
            {
                TempData["ErrorMessage"] = $"Promotional price (RM {Input.DiscountedPrice:N2}) must be lower than original price (RM {food.Price:N2}).";
                return RedirectToPage();
            }

            var startValidation = ValidatePromotionWindow();
            if (startValidation is not null) return startValidation;

            promotion.FoodID = Input.FoodID;
            promotion.DiscountedPrice = Input.DiscountedPrice;
            promotion.StartDateTime = Input.StartDate.ToDateTime(Input.StartTime);
            promotion.EndDateTime = Input.EndDate.ToDateTime(Input.EndTime);

            try
            {
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Promotion {promotion.PromoID} updated successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update promotion.");
                TempData["ErrorMessage"] = "An error occurred while updating the promotion: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeletePromotionAsync(string? promoId)
        {
            if (!User.IsInRole("Administrator"))
            {
                TempData["ErrorMessage"] = "Only administrators are authorized to delete promotions.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(promoId))
            {
                TempData["ErrorMessage"] = "Promotion ID is missing.";
                return RedirectToPage();
            }

            var promotion = await _dbContext.Promotions.FirstOrDefaultAsync(p => p.PromoID == promoId);
            if (promotion == null)
            {
                TempData["ErrorMessage"] = "Promotion not found.";
                return RedirectToPage();
            }

            try
            {
                _dbContext.Promotions.Remove(promotion);
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Promotion {promoId} deleted successfully.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete promotion.");
                TempData["ErrorMessage"] = "An error occurred while deleting the promotion: " + ex.Message;
            }

            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            AvailableFoods = await _dbContext.FoodItems
                .AsNoTracking()
                .OrderBy(f => f.FoodName)
                .ToListAsync();

            var allPromos = await _dbContext.Promotions
                .AsNoTracking()
                .Include(p => p.Food)
                .Include(p => p.Staff)
                    .ThenInclude(s => s.User)
                .OrderByDescending(p => p.StartDateTime)
                .ThenBy(p => p.PromoID)
                .ToListAsync();

            var now = DateTime.Now;

            var rows = allPromos.Select(p =>
            {
                var originalPrice = p.Food?.Price ?? 0m;
                var discountAmount = Math.Max(0, originalPrice - p.DiscountedPrice);
                var discountPercent = originalPrice > 0 ? (int)Math.Round((discountAmount / originalPrice) * 100) : 0;
                var status = PromotionService.GetPromotionStatus(p, now);

                return new PromotionRow(
                    PromoID: p.PromoID,
                    FoodID: p.FoodID,
                    FoodName: p.Food?.FoodName ?? "Unknown Dish",
                    FoodCategory: p.Food?.Category ?? "General",
                    FoodImageUrl: string.IsNullOrWhiteSpace(p.Food?.ImageUrl) ? "/images/placeholder-food.jpg" : p.Food.ImageUrl,
                    OriginalPrice: originalPrice,
                    DiscountedPrice: p.DiscountedPrice,
                    DiscountAmount: discountAmount,
                    DiscountPercent: discountPercent,
                    StartDateTime: p.StartDateTime,
                    EndDateTime: p.EndDateTime,
                    StartDateTimeDisplay: p.StartDateTime.ToString("dd MMM yyyy, hh:mm tt"),
                    EndDateTimeDisplay: p.EndDateTime.ToString("dd MMM yyyy, hh:mm tt"),
                    Status: status,
                    StaffID: p.StaffID,
                    StaffName: p.Staff?.User?.FullName ?? p.StaffID
                );
            }).ToList();

            TotalPromotionsCount = rows.Count;
            ActivePromotionsCount = rows.Count(r => r.Status == "Active");
            UpcomingPromotionsCount = rows.Count(r => r.Status == "Upcoming");
            EndedPromotionsCount = rows.Count(r => r.Status == "Ended");

            var filtered = rows.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                filtered = filtered.Where(r =>
                    r.PromoID.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.FoodName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.FoodCategory.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.StaffName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && !StatusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            Promotions = filtered.ToList();
        }

        private IActionResult? ValidatePromotionWindow()
        {
            var startDateTime = Input.StartDate.ToDateTime(Input.StartTime);
            var endDateTime = Input.EndDate.ToDateTime(Input.EndTime);

            if (endDateTime <= startDateTime)
            {
                ModelState.AddModelError(nameof(Input.EndDateTime), "End date and time must be after the start date and time.");
                TempData["ErrorMessage"] = "End date and time must be after the start date and time.";
                return RedirectToPage();
            }

            return null;
        }

        private async Task<string?> GetCurrentStaffIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await _dbContext.Staffs
                .Where(s => s.UserID == userId)
                .Select(s => s.StaffID)
                .FirstOrDefaultAsync();
        }

        private async Task<string> GenerateNextPromoIdAsync()
        {
            var existingIds = await _dbContext.Promotions
                .AsNoTracking()
                .Select(p => p.PromoID)
                .ToListAsync();

            var maxNumber = 0;
            foreach (var id in existingIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                string numberPart = id;
                if (id.StartsWith("PRM-", StringComparison.OrdinalIgnoreCase))
                {
                    numberPart = id.Substring(4);
                }
                else if (id.StartsWith("PRM", StringComparison.OrdinalIgnoreCase))
                {
                    numberPart = id.Substring(3);
                }
                else if (id.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                {
                    numberPart = id.Substring(1);
                }

                if (int.TryParse(numberPart, out int num))
                {
                    if (num > maxNumber) maxNumber = num;
                }
            }

            return $"PRM-{(maxNumber + 1):D3}";
        }

        public sealed record PromotionRow(
            string PromoID,
            string FoodID,
            string FoodName,
            string FoodCategory,
            string FoodImageUrl,
            decimal OriginalPrice,
            decimal DiscountedPrice,
            decimal DiscountAmount,
            int DiscountPercent,
            DateTime StartDateTime,
            DateTime EndDateTime,
            string StartDateTimeDisplay,
            string EndDateTimeDisplay,
            string Status,
            string StaffID,
            string StaffName);

        public class PromoFormModel
        {
            public string? PromoID { get; set; }

            [Required(ErrorMessage = "Please select a food item.")]
            public string FoodID { get; set; } = string.Empty;

            [Required(ErrorMessage = "Enter the discounted price.")]
            [Range(0.01, 10000.00, ErrorMessage = "Discounted price must be greater than zero.")]
            public decimal DiscountedPrice { get; set; }

            [Required(ErrorMessage = "Select start date.")]
            public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

            [Required(ErrorMessage = "Select start time.")]
            public TimeOnly StartTime { get; set; } = new TimeOnly(0, 0);

            [Required(ErrorMessage = "Select end date.")]
            public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

            [Required(ErrorMessage = "Select end time.")]
            public TimeOnly EndTime { get; set; } = new TimeOnly(23, 59);

            public DateTime StartDateTime => StartDate.ToDateTime(StartTime);
            public DateTime EndDateTime => EndDate.ToDateTime(EndTime);
        }
    }
}

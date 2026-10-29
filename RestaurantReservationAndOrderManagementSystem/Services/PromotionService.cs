using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Services
{
    public class PromotionService
    {
        private readonly ApplicationDbContext _context;

        public PromotionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Promotion>> GetAllPromotionsAsync()
        {
            return await _context.Promotions
                .Include(p => p.Food)
                .Include(p => p.Staff)
                .OrderByDescending(p => p.StartDateTime)
                .ThenBy(p => p.EndDateTime)
                .ToListAsync();
        }

        public async Task<IReadOnlyDictionary<string, Promotion>> GetActivePromotionsMapAsync(DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.Now;

            var candidatePromotions = await _context.Promotions
                .Include(p => p.Food)
                .Where(p => p.StartDateTime <= now && p.EndDateTime >= now)
                .ToListAsync();

            var activePromos = new Dictionary<string, Promotion>(StringComparer.OrdinalIgnoreCase);

            foreach (var promo in candidatePromotions)
            {

                // Promo discount must be strictly lower than regular price to be beneficial
                if (promo.Food != null && promo.DiscountedPrice >= promo.Food.Price)
                {
                    continue;
                }

                if (!activePromos.TryGetValue(promo.FoodID, out var existingPromo))
                {
                    activePromos[promo.FoodID] = promo;
                }
                else if (promo.DiscountedPrice < existingPromo.DiscountedPrice)
                {
                    activePromos[promo.FoodID] = promo;
                }
            }

            return activePromos;
        }

        public async Task<Promotion?> GetActivePromotionForFoodAsync(string foodId, DateTime? asOf = null)
        {
            if (string.IsNullOrWhiteSpace(foodId))
            {
                return null;
            }

            var map = await GetActivePromotionsMapAsync(asOf);
            return map.GetValueOrDefault(foodId);
        }

        public PromotionOrderPricing CalculateOrderPricing(
            IEnumerable<Order_Items> orderItems,
            IReadOnlyDictionary<string, Promotion> activePromotions)
        {
            return CalculateOrderPricing(
                orderItems.Select(item => (item.Food, item.Quantity)),
                activePromotions);
        }

        public async Task<PromotionOrderPricing> ResolvePaymentAlignedPricingAsync(
            IEnumerable<(Food_Items food, int quantity)> items,
            DateTime promotionTime,
            decimal? recordedAmount = null,
            decimal? recordedDiscount = null)
        {
            var itemList = items.ToList();
            var promotions = await GetActivePromotionsMapAsync(promotionTime);
            var promotedPricing = CalculateOrderPricing(itemList, promotions);

            if (!recordedAmount.HasValue)
            {
                return promotedPricing;
            }

            var discountMatches = !recordedDiscount.HasValue ||
                                  Math.Abs(recordedDiscount.Value - promotedPricing.TotalDiscount) <= 0.01m;

            if (Math.Abs(recordedAmount.Value - promotedPricing.Total) <= 0.01m && discountMatches)
            {
                return promotedPricing;
            }

            var regularPricing = CalculateOrderPricing(itemList, new Dictionary<string, Promotion>());
            if (Math.Abs(recordedAmount.Value - regularPricing.Total) <= 0.01m &&
                (!recordedDiscount.HasValue || recordedDiscount.Value == 0m))
            {
                return regularPricing;
            }

            // A transaction without matching promoted or regular pricing must not inherit
            // a promotion retroactively from a newer promotion schedule.
            return regularPricing;
        }

        public PromotionOrderPricing CalculateOrderPricing(
            IEnumerable<(Food_Items food, int quantity)> items,
            IReadOnlyDictionary<string, Promotion> activePromotions)
        {
            var pricingItems = new List<PromotionPricingItem>();

            foreach (var (food, quantity) in items)
            {
                if (food == null || quantity <= 0)
                {
                    continue;
                }

                var hasPromo = activePromotions.TryGetValue(food.FoodID, out var promo) &&
                               promo.DiscountedPrice < food.Price;

                var effectiveUnitPrice = hasPromo ? promo!.DiscountedPrice : food.Price;
                var unitDiscount = hasPromo ? Math.Max(0, food.Price - promo!.DiscountedPrice) : 0m;
                var lineDiscount = unitDiscount * quantity;
                var lineTotal = effectiveUnitPrice * quantity;

                pricingItems.Add(new PromotionPricingItem(
                    FoodId: food.FoodID,
                    FoodName: food.FoodName,
                    Quantity: quantity,
                    OriginalUnitPrice: food.Price,
                    EffectiveUnitPrice: effectiveUnitPrice,
                    UnitDiscount: unitDiscount,
                    LineTotal: lineTotal,
                    LineDiscount: lineDiscount,
                    HasPromotion: hasPromo,
                    PromoId: hasPromo ? promo!.PromoID : null
                ));
            }

            var originalSubtotal = pricingItems.Sum(item => item.OriginalUnitPrice * item.Quantity);
            var totalDiscount = pricingItems.Sum(item => item.LineDiscount);
            var netSubtotal = pricingItems.Sum(item => item.LineTotal);
            var tax = Math.Round(netSubtotal * 0.10m, 2, MidpointRounding.AwayFromZero);
            var total = netSubtotal + tax;

            return new PromotionOrderPricing(
                Items: pricingItems,
                OriginalSubtotal: originalSubtotal,
                TotalDiscount: totalDiscount,
                NetSubtotal: netSubtotal,
                Tax: tax,
                Total: total
            );
        }

        public static string GetPromotionStatus(Promotion p, DateTime? asOf = null)
        {
            var now = asOf ?? DateTime.Now;

            if (now < p.StartDateTime)
            {
                return "Upcoming";
            }

            if (now > p.EndDateTime)
            {
                return "Ended";
            }

            return "Active";
        }
    }

    public sealed record PromotionPricingItem(
        string FoodId,
        string FoodName,
        int Quantity,
        decimal OriginalUnitPrice,
        decimal EffectiveUnitPrice,
        decimal UnitDiscount,
        decimal LineTotal,
        decimal LineDiscount,
        bool HasPromotion,
        string? PromoId);

    public sealed record PromotionOrderPricing(
        IReadOnlyList<PromotionPricingItem> Items,
        decimal OriginalSubtotal,
        decimal TotalDiscount,
        decimal NetSubtotal,
        decimal Tax,
        decimal Total);
}

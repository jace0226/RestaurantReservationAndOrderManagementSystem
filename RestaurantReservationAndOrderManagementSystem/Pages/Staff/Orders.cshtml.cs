using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class OrdersModel : PageModel
    {
        private static readonly string[] OrderStatuses =
        [
            "Pending",
            "Preparing",
            "Ready to Serve",
            "Served",
            "Payment",
            "Completed",
            "Cancelled"
        ];

        private readonly ApplicationDbContext _dbContext;
        private readonly PromotionService _promotionService;
        private readonly ILogger<OrdersModel> _logger;

        public OrdersModel(
            ApplicationDbContext dbContext,
            PromotionService promotionService,
            ILogger<OrdersModel> logger)
        {
            _dbContext = dbContext;
            _promotionService = promotionService;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string StatusFilter { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public string? OrderId { get; set; }

        [BindProperty]
        public string Status { get; set; } = string.Empty;

        public IReadOnlyList<string> Statuses { get; private set; } = OrderStatuses;
        public IReadOnlyList<OrderRow> Orders { get; private set; } = [];
        public OrderDetails? SelectedOrder { get; private set; }

        public async Task OnGetAsync()
        {
            await LoadOrdersAsync();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(
            string? orderId,
            string? status,
            string? statusFilter)
        {
            if (string.IsNullOrWhiteSpace(orderId) ||
                status is null ||
                (!OrderStatuses.Contains(status, StringComparer.Ordinal) &&
                 !await _dbContext.Orders.AnyAsync(item => item.OrderStatus == status)))
            {
                return BadRequest();
            }

            var order = await _dbContext.Orders
                .SingleOrDefaultAsync(item => item.OrderID == orderId);
            if (order is null)
            {
                return NotFound();
            }

            order.OrderStatus = status;
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not update order status for order {OrderId}.", orderId);
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return RedirectToPage(new { orderId, statusFilter });
        }

        private async Task LoadOrdersAsync()
        {
            var sourceOrders = await _dbContext.Orders
                .AsNoTracking()
                .Include(order => order.Customer)
                    .ThenInclude(customer => customer.User)
                .Include(order => order.Table)
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync();

            var orderIds = sourceOrders.Select(order => order.OrderID).ToArray();
            Statuses = OrderStatuses;
            if (StatusFilter != "All" &&
                !Statuses.Contains(StatusFilter, StringComparer.OrdinalIgnoreCase))
            {
                StatusFilter = "All";
            }

            List<Order_Items> items = orderIds.Length == 0
                ? []
                : await _dbContext.OrderItems
                    .AsNoTracking()
                    .Where(item => orderIds.Contains(item.OrderID))
                    .Include(item => item.Food)
                    .ToListAsync();

            var transactionRecords = orderIds.Length == 0
                ? new Dictionary<string, Transaction>()
                : await _dbContext.Transactions
                    .AsNoTracking()
                    .Where(transaction => orderIds.Contains(transaction.OrderID))
                    .ToDictionaryAsync(transaction => transaction.OrderID);

            var itemsByOrder = items
                .GroupBy(item => item.OrderID)
                .ToDictionary(group => group.Key, group => group.ToList());

            // Build each order row using promotions that were active at the order's creation time
            var allRows = new List<OrderRow>();
            foreach (var order in sourceOrders)
            {
                var orderItems = itemsByOrder.GetValueOrDefault(order.OrderID) ?? [];
                var hasRecordedTotal = transactionRecords.TryGetValue(order.OrderID, out var transaction);
                var paymentTime = hasRecordedTotal
                    ? transaction!.TransactionDate.ToDateTime(transaction.TransactionTime)
                    : DateTime.Now;
                var pricing = await _promotionService.ResolvePaymentAlignedPricingAsync(
                    orderItems.Select(oi => (oi.Food, oi.Quantity)),
                    paymentTime,
                    hasRecordedTotal ? transaction!.TransactionAmount : null,
                    hasRecordedTotal ? transaction!.DiscountAmount : null);

                var effectiveTotal = hasRecordedTotal ? transaction!.TransactionAmount : pricing.Total;

                allRows.Add(new OrderRow(
                    order.OrderID,
                    order.CreatedAt,
                    order.Table.TableID,
                    order.Customer.User.FullName,
                    order.OrderStatus,
                    orderItems.Sum(item => item.Quantity),
                    effectiveTotal,
                    hasRecordedTotal,
                    pricing.TotalDiscount > 0,
                    pricing.TotalDiscount));
            }

            var filteredRows = StatusFilter == "All"
                ? allRows
                : allRows.Where(order => string.Equals(
                    order.Status,
                    StatusFilter,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            Orders = filteredRows;

            var selected = filteredRows.FirstOrDefault(order => order.OrderId == OrderId)
                ?? filteredRows.FirstOrDefault();
            if (selected is null)
            {
                SelectedOrder = null;
                return;
            }

            OrderId = selected.OrderId;
            var selectedItems = itemsByOrder.GetValueOrDefault(selected.OrderId) ?? [];

            var selectedTransaction = transactionRecords.GetValueOrDefault(selected.OrderId);
            var selectedPaymentTime = selectedTransaction is null
                ? DateTime.Now
                : selectedTransaction.TransactionDate.ToDateTime(selectedTransaction.TransactionTime);
            var selectedPricing = await _promotionService.ResolvePaymentAlignedPricingAsync(
                selectedItems.Select(oi => (oi.Food, oi.Quantity)),
                selectedPaymentTime,
                selectedTransaction?.TransactionAmount,
                selectedTransaction?.DiscountAmount);

            var finalSelectedTotal = selected.HasRecordedTotal ? selected.Total : selectedPricing.Total;

            SelectedOrder = new OrderDetails(
                selected.OrderId,
                selected.CreatedAt,
                selected.TableId,
                selected.CustomerName,
                selected.Status,
                selected.ItemCount,
                selectedPricing.OriginalSubtotal,
                selectedPricing.TotalDiscount,
                selectedPricing.NetSubtotal,
                selectedPricing.Tax,
                finalSelectedTotal,
                selected.HasRecordedTotal,
                selectedPricing.TotalDiscount > 0,
                selectedPricing.Items.Select(item => new OrderItemRow(
                    item.FoodName,
                    item.OriginalUnitPrice,
                    item.EffectiveUnitPrice,
                    item.Quantity,
                    item.LineTotal,
                    item.LineDiscount,
                    item.HasPromotion,
                    item.PromoId)).ToList());
        }

        public sealed record OrderRow(
            string OrderId,
            DateTime CreatedAt,
            string TableId,
            string CustomerName,
            string Status,
            int ItemCount,
            decimal Total,
            bool HasRecordedTotal,
            bool HasPromotions,
            decimal TotalDiscount);

        public sealed record OrderDetails(
            string OrderId,
            DateTime CreatedAt,
            string TableId,
            string CustomerName,
            string Status,
            int ItemCount,
            decimal OriginalSubtotal,
            decimal TotalDiscount,
            decimal NetSubtotal,
            decimal Tax,
            decimal Total,
            bool HasRecordedTotal,
            bool HasPromotions,
            IReadOnlyList<OrderItemRow> Items);

        public sealed record OrderItemRow(
            string Name,
            decimal OriginalUnitPrice,
            decimal EffectiveUnitPrice,
            int Quantity,
            decimal LineTotal,
            decimal LineDiscount,
            bool HasPromotion,
            string? PromoId);
    }
}

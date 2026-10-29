using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    [Authorize(Roles = "Staff,Administrator")]
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

        [BindProperty(SupportsGet = true)]
        public DateTime SelectedDate { get; set; } = DateTime.Today;

        [BindProperty]
        public string Status { get; set; } = string.Empty;

        [TempData]
        public string? PaymentSuccess { get; set; }

        [TempData]
        public string? PaymentError { get; set; }

        public IReadOnlyList<string> Statuses { get; private set; } = OrderStatuses;
        public IReadOnlyList<OrderRow> Orders { get; private set; } = [];
        public OrderDetails? SelectedOrder { get; private set; }

        public async Task OnGetAsync(DateTime? selectedDate)
        {
            SelectedDate = selectedDate.HasValue && selectedDate.Value != default
                ? selectedDate.Value.Date
                : DateTime.Today;
            await LoadOrdersAsync();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(
            string? orderId,
            string? status,
            string? statusFilter,
            DateTime? selectedDate)
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

            if (status == "Completed" &&
                !await _dbContext.Transactions.AnyAsync(item => item.OrderID == orderId))
            {
                PaymentError = "Record payment before marking this order as completed.";
                return RedirectToPage(new { orderId, statusFilter, selectedDate });
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

            return RedirectToPage(new { orderId, statusFilter, selectedDate });
        }

        public async Task<IActionResult> OnPostRecordCounterPaymentAsync(
            string? orderId,
            decimal tenderedAmount,
            string? statusFilter,
            DateTime? selectedDate)
        {
            if (string.IsNullOrWhiteSpace(orderId) ||
                tenderedAmount <= 0 ||
                tenderedAmount > 9999999999999999.99m ||
                decimal.Round(tenderedAmount, 2) != tenderedAmount ||
                (!User.IsInRole("Staff") && !User.IsInRole("Administrator")))
            {
                return BadRequest();
            }

            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

            var order = await _dbContext.Orders
                .SingleOrDefaultAsync(item => item.OrderID == orderId);
            if (order is null)
            {
                return NotFound();
            }

            if (order.OrderStatus != "Payment" ||
                order.PaymentMethod != "Counter" ||
                await _dbContext.Transactions.AnyAsync(item => item.OrderID == orderId))
            {
                PaymentError = "This order is not awaiting counter payment.";
                return RedirectToPage(new { orderId, statusFilter, selectedDate });
            }

            var items = await _dbContext.OrderItems
                .AsNoTracking()
                .Where(item => item.OrderID == orderId)
                .Include(item => item.Food)
                .ToListAsync();
            var paidAt = DateTime.Now;
            var pricing = await _promotionService.ResolvePaymentAlignedPricingAsync(
                items.Select(item => (item.Food, item.Quantity)),
                paidAt);

            if (pricing.Total <= 0)
            {
                PaymentError = "This order has an invalid payment total.";
                return RedirectToPage(new { orderId, statusFilter, selectedDate });
            }

            if (tenderedAmount < pricing.Total)
            {
                PaymentError = $"Amount received must be at least RM {pricing.Total:N2}.";
                return RedirectToPage(new { orderId, statusFilter, selectedDate });
            }

            order.OrderStatus = "Completed";
            order.CompletedAt = paidAt;
            _dbContext.Transactions.Add(new Transaction
            {
                TransactionID = $"CTR-{Guid.NewGuid():N}",
                OrderID = orderId,
                TransactionType = "Counter",
                TransactionDate = DateOnly.FromDateTime(paidAt),
                TransactionTime = TimeOnly.FromDateTime(paidAt),
                TransactionAmount = pricing.Total,
                OriginalSubtotal = pricing.OriginalSubtotal,
                DiscountAmount = pricing.TotalDiscount,
                NetSubtotal = pricing.NetSubtotal,
                TaxAmount = pricing.Tax,
                TenderedAmount = tenderedAmount,
                ChangeGiven = tenderedAmount - pricing.Total
            });

            try
            {
                await _dbContext.SaveChangesAsync();
                await databaseTransaction.CommitAsync();
            }
            catch (DbUpdateException exception)
            {
                await databaseTransaction.RollbackAsync();
                _logger.LogError(exception, "Could not record counter payment for order {OrderId}.", orderId);
                PaymentError = "The counter payment could not be recorded. Please try again.";
                return RedirectToPage(new { orderId, statusFilter, selectedDate });
            }

            PaymentSuccess = $"Counter payment recorded. Change due: RM {tenderedAmount - pricing.Total:N2}.";
            return RedirectToPage(new { orderId, statusFilter, selectedDate });
        }

        private async Task LoadOrdersAsync()
        {
            var startOfDay = SelectedDate.Date;
            var endOfDay = startOfDay.AddDays(1);
            var sourceOrders = await _dbContext.Orders
                .AsNoTracking()
                .Include(order => order.Customer)
                    .ThenInclude(customer => customer.User)
                .Include(order => order.Table)
                .Where(order => order.CreatedAt >= startOfDay && order.CreatedAt < endOfDay)
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
                    pricing.TotalDiscount,
                    order.PaymentMethod));
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
                selected.PaymentMethod,
                selectedTransaction?.TransactionType,
                selectedTransaction?.TenderedAmount,
                selectedTransaction?.ChangeGiven,
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
            decimal TotalDiscount,
            string? PaymentMethod);

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
            string? PaymentMethod,
            string? RecordedPaymentMethod,
            decimal? TenderedAmount,
            decimal? ChangeGiven,
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

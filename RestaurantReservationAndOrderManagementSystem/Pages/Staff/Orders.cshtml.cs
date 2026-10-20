using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

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
            "Cancelled",
            "Cancel"
        ];

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<OrdersModel> _logger;

        public OrdersModel(ApplicationDbContext dbContext, ILogger<OrdersModel> logger)
        {
            _dbContext = dbContext;
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
            Statuses = OrderStatuses
                .Concat(sourceOrders.Select(order => order.OrderStatus))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            var transactionTotals = orderIds.Length == 0
                ? new Dictionary<string, decimal>()
                : await _dbContext.Transactions
                    .AsNoTracking()
                    .Where(transaction => orderIds.Contains(transaction.OrderID))
                    .ToDictionaryAsync(transaction => transaction.OrderID, transaction => transaction.TransactionAmount);
            var itemsByOrder = items
                .GroupBy(item => item.OrderID)
                .ToDictionary(group => group.Key, group => group.ToList());

            var allRows = sourceOrders.Select(order =>
            {
                var orderItems = itemsByOrder.GetValueOrDefault(order.OrderID) ?? [];
                var hasRecordedTotal = transactionTotals.TryGetValue(order.OrderID, out var recordedTotal);
                return new OrderRow(
                    order.OrderID,
                    order.CreatedAt,
                    order.Table.TableID,
                    order.Customer.User.FullName,
                    order.OrderStatus,
                    orderItems.Sum(item => item.Quantity),
                    hasRecordedTotal ? recordedTotal : orderItems.Sum(item => item.Food.Price * item.Quantity),
                    hasRecordedTotal);
            }).ToList();

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
            SelectedOrder = new OrderDetails(
                selected.OrderId,
                selected.CreatedAt,
                selected.TableId,
                selected.CustomerName,
                selected.Status,
                selected.ItemCount,
                selected.Total,
                selected.HasRecordedTotal,
                selectedItems.Select(item => new OrderItemRow(
                    item.Food.FoodName,
                    item.Food.Price,
                    item.Quantity)).ToList());
        }

        public sealed record OrderRow(
            string OrderId,
            DateTime CreatedAt,
            string TableId,
            string CustomerName,
            string Status,
            int ItemCount,
            decimal Total,
            bool HasRecordedTotal);

        public sealed record OrderDetails(
            string OrderId,
            DateTime CreatedAt,
            string TableId,
            string CustomerName,
            string Status,
            int ItemCount,
            decimal Total,
            bool HasRecordedTotal,
            IReadOnlyList<OrderItemRow> Items);

        public sealed record OrderItemRow(string Name, decimal UnitPrice, int Quantity);
    }
}

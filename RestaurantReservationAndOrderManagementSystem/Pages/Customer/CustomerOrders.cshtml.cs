using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    [Authorize(Roles = "Customer")]
    public class CustomerOrdersModel : PageModel
    {
        private const string CurrentCategory = "Current";
        private const string HistoryCategory = "History";
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CustomerOrdersModel> _logger;

        public CustomerOrdersModel(
            ApplicationDbContext dbContext,
            ILogger<CustomerOrdersModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string Category { get; set; } = CurrentCategory;

        [BindProperty(SupportsGet = true)]
        public DateOnly? DateFilter { get; set; }

        [TempData]
        public string? OrderSuccess { get; set; }

        [TempData]
        public string? OrderError { get; set; }

        public IReadOnlyList<OrderView> Orders { get; private set; } = [];

        public async Task<IActionResult> OnGetAsync()
        {
            Category = NormalizeCategory(Category);
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId is null)
            {
                return NotFound();
            }

            await LoadOrdersAsync(customerId);
            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(
            string? orderId,
            string? category,
            DateOnly? dateFilter)
        {
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(orderId))
            {
                OrderError = "That order could not be found.";
                return RedirectToPage(new { category = NormalizeCategory(category), dateFilter });
            }

            var order = await _dbContext.Orders
                .SingleOrDefaultAsync(item =>
                    item.OrderID == orderId &&
                    item.CustomerID == customerId);

            if (order is null)
            {
                OrderError = "That order could not be found.";
                return RedirectToPage(new { category = NormalizeCategory(category), dateFilter });
            }

            if (!string.Equals(order.OrderStatus, "Pending", StringComparison.Ordinal))
            {
                OrderError = "Only pending orders can be cancelled.";
                return RedirectToPage(new { category = NormalizeCategory(category), dateFilter });
            }

            try
            {
                var updatedOrders = await _dbContext.Orders
                    .Where(item =>
                        item.OrderID == orderId &&
                        item.CustomerID == customerId &&
                        item.OrderStatus == "Pending")
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.OrderStatus, "Cancelled"));

                if (updatedOrders != 1)
                {
                    OrderError = "This order is no longer pending and could not be cancelled.";
                    return RedirectToPage(new { category = NormalizeCategory(category), dateFilter });
                }
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not cancel order {OrderId} for customer {CustomerId}.", orderId, customerId);
                OrderError = "We could not cancel your order. Please try again.";
                return RedirectToPage(new { category = NormalizeCategory(category), dateFilter });
            }

            OrderSuccess = "Your order has been cancelled.";
            return RedirectToPage(new { category = HistoryCategory, dateFilter });
        }

        private async Task<string?> GetCurrentCustomerIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await _dbContext.Customers
                .Where(customer => customer.UserID == userId)
                .Select(customer => customer.CustomerID)
                .SingleOrDefaultAsync();
        }

        private async Task LoadOrdersAsync(string customerId)
        {
            var query = _dbContext.Orders
                .AsNoTracking()
                .Where(order => order.CustomerID == customerId);

            if (Category == CurrentCategory)
            {
                query = query.Where(order =>
                    order.OrderStatus != "Completed" &&
                    order.OrderStatus != "Cancelled");
            }
            else
            {
                query = query.Where(order =>
                    order.OrderStatus == "Completed" ||
                    order.OrderStatus == "Cancelled");
            }

            if (DateFilter.HasValue)
            {
                var start = DateFilter.Value.ToDateTime(TimeOnly.MinValue);
                var end = start.AddDays(1);
                query = query.Where(order => order.CreatedAt >= start && order.CreatedAt < end);
            }

            var orders = await query
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync();

            if (orders.Count == 0)
            {
                Orders = [];
                return;
            }

            var orderIds = orders.Select(order => order.OrderID).ToArray();
            var itemsByOrder = await _dbContext.OrderItems
                .AsNoTracking()
                .Where(item => orderIds.Contains(item.OrderID))
                .Include(item => item.Food)
                .ToListAsync();

            var groupedItems = itemsByOrder
                .GroupBy(item => item.OrderID)
                .ToDictionary(group => group.Key, group => group.ToList());

            Orders = orders.Select(order =>
            {
                var items = groupedItems.GetValueOrDefault(order.OrderID) ?? [];
                var viewItems = items.Select(item => new OrderItemView(
                    item.Quantity,
                    item.Food.FoodName,
                    item.Food.Price)).ToList();
                var subtotal = viewItems.Sum(item => item.Price * item.Quantity);
                var tax = Math.Round(subtotal * 0.10m, 2, MidpointRounding.AwayFromZero);

                return new OrderView(
                    order.OrderID,
                    order.CreatedAt,
                    order.OrderStatus,
                    viewItems,
                    subtotal,
                    tax,
                    subtotal + tax);
            }).ToList();
        }

        private static string NormalizeCategory(string? category) =>
            string.Equals(category, HistoryCategory, StringComparison.OrdinalIgnoreCase)
                ? HistoryCategory
                : CurrentCategory;

        public sealed record OrderView(
            string OrderId,
            DateTime CreatedAt,
            string Status,
            IReadOnlyList<OrderItemView> Items,
            decimal Subtotal,
            decimal Tax,
            decimal Total);

        public sealed record OrderItemView(int Quantity, string Name, decimal Price);
    }
}

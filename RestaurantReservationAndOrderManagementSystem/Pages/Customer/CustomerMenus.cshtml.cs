using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using CustomerEntity = RestaurantReservationAndOrderManagementSystem.Models.Customer;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    [Authorize(Roles = "Customer")]
    public class CustomerMenusModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CustomerMenusModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Food_Items> MenuItems { get; set; } = new();
        public List<Table> AvailableTables { get; set; } = new();
        public object? ActiveBooking { get; set; }
        public string? CurrentCustomerId { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Challenge();
            }

            CurrentCustomerId = customer.CustomerID;
            var today = DateOnly.FromDateTime(DateTime.Now);

            // SCENARIO A: Find active booking for today with an assigned TableID
            var activeBookingEntity = await _context.Bookings
                .Include(b => b.Table)
                .FirstOrDefaultAsync(b =>
                    b.CustomerID == CurrentCustomerId &&
                    b.BookingDate == today &&
                    (b.BookingStatus == "Confirmed" || b.BookingStatus == "Arriving" || b.BookingStatus == "Seated") &&
                    !string.IsNullOrEmpty(b.TableID));

            if (activeBookingEntity != null)
            {
                ActiveBooking = new
                {
                    bookingId = activeBookingEntity.BookingID,
                    tableId = activeBookingEntity.TableID
                };
            }
            else
            {
                ActiveBooking = null; // SCENARIO B: Fallback to walk-in table selection
            }

            // 1. Fetch all food items directly from DB
            MenuItems = await _context.FoodItems.ToListAsync();

            // 2. Compute dynamic recommendations
            bool hasAnyOrderData = await _context.OrderItems.AnyAsync();
            List<string> recommendedFoodIds = new();
            string dynamicLabel = "Chef's Pick";

            if (!hasAnyOrderData)
            {
                recommendedFoodIds = MenuItems
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .Select(f => f.FoodID)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToList()!;
            }
            else
            {
                bool hasUserOrders = await _context.Orders.AnyAsync(o => o.CustomerID == CurrentCustomerId);
                if (hasUserOrders)
                {
                    string topFoodId = await _context.OrderItems
                        .Where(oi => oi.Order.CustomerID == CurrentCustomerId)
                        .GroupBy(oi => oi.FoodID)
                        .OrderByDescending(g => g.Sum(oi => oi.Quantity))
                        .Select(g => g.Key)
                        .FirstOrDefaultAsync() ?? string.Empty;

                    if (!string.IsNullOrEmpty(topFoodId)) recommendedFoodIds.Add(topFoodId);
                    dynamicLabel = "Your Favorite";
                }
                else
                {
                    string topFoodId = await _context.OrderItems
                        .GroupBy(oi => oi.FoodID)
                        .OrderByDescending(g => g.Sum(oi => oi.Quantity))
                        .Select(g => g.Key)
                        .FirstOrDefaultAsync() ?? string.Empty;

                    if (!string.IsNullOrEmpty(topFoodId)) recommendedFoodIds.Add(topFoodId);
                    dynamicLabel = "Popular Choice";
                }
            }

            // 3. Assign recommendation label
            foreach (var item in MenuItems)
            {
                if (!string.IsNullOrEmpty(item.FoodID) && recommendedFoodIds.Contains(item.FoodID))
                {
                    item.Recommendation = dynamicLabel;
                }
            }

            // 4. Load available tables for walk-ins
            AvailableTables = await _context.Tables
                .Where(t => t.TableAvailability == "On dine-in")
                .ToListAsync();

            return Page();
        }

        // --- POST Handler: Create Order ---
        public async Task<IActionResult> OnPostCreateOrderAsync([FromBody] CreateOrderRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return new JsonResult(new { success = false, message = "Cart is empty." }) { StatusCode = 400 };
            }

            if (string.IsNullOrEmpty(request.TableId))
            {
                return new JsonResult(new { success = false, message = "Table ID is required." }) { StatusCode = 400 };
            }

            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return new JsonResult(new { success = false, message = "User not authenticated." }) { StatusCode = 401 };
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Generate sequential Order ID: ORD-001, ORD-002, etc.
                var existingOrderIds = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.OrderID != null && o.OrderID.StartsWith("ORD-"))
                    .Select(o => o.OrderID!)
                    .ToListAsync();

                int maxOrderNum = 0;
                foreach (var id in existingOrderIds)
                {
                    if (id.Length > 4 && int.TryParse(id.Substring(4), out int parsedNum))
                    {
                        if (parsedNum > maxOrderNum) maxOrderNum = parsedNum;
                    }
                }
                string newOrderId = $"ORD-{(maxOrderNum + 1):D3}";

                // 1. Create and add Order header
                var order = new Order
                {
                    OrderID = newOrderId,
                    CustomerID = customer.CustomerID,
                    TableID = request.TableId,
                    BookingID = string.IsNullOrWhiteSpace(request.BookingId) ? null : request.BookingId,
                    StaffID = null,
                    OrderStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    CompletedAt = null
                };

                _context.Orders.Add(order);

                // Generate sequential OrderItemID: OIT-001, OIT-002, etc.
                var existingOrderItemIds = await _context.OrderItems
                    .AsNoTracking()
                    .Where(oi => oi.OrderItemID != null && oi.OrderItemID.StartsWith("OIT-"))
                    .Select(oi => oi.OrderItemID!)
                    .ToListAsync();

                int maxOrderItemNum = 0;
                foreach (var id in existingOrderItemIds)
                {
                    if (id.Length > 4 && int.TryParse(id.Substring(4), out int parsedItemNum))
                    {
                        if (parsedItemNum > maxOrderItemNum) maxOrderItemNum = parsedItemNum;
                    }
                }
                int nextOrderItemNumber = maxOrderItemNum + 1;

                // 2. Add each order item separately
                foreach (var cartItem in request.Items)
                {
                    string newOrderItemId = $"OIT-{nextOrderItemNumber:D3}";
                    nextOrderItemNumber++;

                    var orderItem = new Order_Items
                    {
                        OrderItemID = newOrderItemId,
                        OrderID = newOrderId,
                        FoodID = cartItem.FoodId,
                        Quantity = cartItem.Quantity,
                        FoodRemarks = cartItem.Remarks
                    };

                    _context.OrderItems.Add(orderItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new JsonResult(new { success = true, orderId = newOrderId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new JsonResult(new { success = false, message = "Database error: " + ex.Message }) { StatusCode = 500 };
            }
        }

        private async Task<CustomerEntity?> GetCurrentCustomerAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await _context.Customers
                .Include(c => c.User)
                .SingleOrDefaultAsync(c => c.UserID == userId);
        }
    }

    public class CreateOrderRequest
    {
        public string TableId { get; set; } = string.Empty;
        public string? BookingId { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }

    public class CartItemDto
    {
        public string FoodId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Remarks { get; set; }
    }
}
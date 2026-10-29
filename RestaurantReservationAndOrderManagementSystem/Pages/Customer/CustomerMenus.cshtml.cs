using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;
using CustomerEntity = RestaurantReservationAndOrderManagementSystem.Models.Customer;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    [Authorize(Roles = "Customer")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)] // Prevent browser caching table state
    public class CustomerMenusModel : PageModel
    {
        private const string SelectedTableSessionKey = "SelectedTableID";
        private const string TableOwnerUserIdSessionKey = "SelectedTableOwnerUserID";
        private const string WrongTableMessage = "This QR code is for a different table. Please scan the QR code for your assigned table.";
        private const string TableSessionExpiredMessage =
            "Your table session has expired or this table has been claimed by another customer. Please scan the QR code on your table again before placing an order.";
        private static readonly string[] EligibleCustomerCheckInBookingStatuses = ["Confirmed", "Arriving", "On dine-in"];

        private readonly ApplicationDbContext _context;
        private readonly TableSessionStore _tableSessions;
        private readonly PromotionService _promotionService;
        private readonly ILogger<CustomerMenusModel> _logger;

        public CustomerMenusModel(
            ApplicationDbContext context,
            TableSessionStore tableSessions,
            PromotionService promotionService,
            ILogger<CustomerMenusModel> logger)
        {
            _context = context;
            _tableSessions = tableSessions;
            _promotionService = promotionService;
            _logger = logger;
        }

        public List<Food_Items> MenuItems { get; set; } = new();
        public List<Table> AvailableTables { get; set; } = new();
        public object? ActivePromotions { get; private set; }
        public string? CurrentCustomerId { get; private set; }
        public string? SelectedTableId { get; private set; }
        public string? TableSessionMessage { get; private set; }
        public object? ActiveBooking { get; private set; }
        public bool CanPlaceOrder { get; private set; }

        public async Task<IActionResult> OnGetAsync(string? tableID)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return Challenge();
            }

            CurrentCustomerId = customer.CustomerID;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Clean up stale session if user ID changed on same browser
            var sessionOwner = HttpContext.Session.GetString(TableOwnerUserIdSessionKey);
            if (!string.IsNullOrEmpty(sessionOwner) && !string.Equals(sessionOwner, userId, StringComparison.Ordinal))
            {
                HttpContext.Session.Remove(SelectedTableSessionKey);
                HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
            }

            var activeBooking = await FindActiveBookingAsync(customer.CustomerID);
            if (activeBooking is not null)
            {
                if (!string.IsNullOrWhiteSpace(tableID) &&
                    !string.Equals(activeBooking.TableID, tableID, StringComparison.OrdinalIgnoreCase))
                {
                    HttpContext.Session.Remove(SelectedTableSessionKey);
                    HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                    ActiveBooking = null;
                    SelectedTableId = null;
                    CanPlaceOrder = false;
                    TableSessionMessage = WrongTableMessage;
                }
                else
                {
                    HttpContext.Session.Remove(SelectedTableSessionKey);
                    HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                    ActiveBooking = new
                    {
                        bookingId = activeBooking.BookingID,
                        tableId = activeBooking.TableID
                    };
                    SelectedTableId = activeBooking.TableID;
                    CanPlaceOrder = activeBooking.Table is not null;
                    if (!CanPlaceOrder)
                    {
                        TableSessionMessage = "Your active booking's table could not be found. Please contact restaurant staff.";
                    }
                }
            }
            else
            {
                await ResolveTableSessionAsync(tableID, customer, userId);
            }

            // Fetch menu items
            MenuItems = await _context.FoodItems.ToListAsync();

            // Load active promotions.
            var activePromosMap = await _promotionService.GetActivePromotionsMapAsync();
            ActivePromotions = activePromosMap.Values.Select(p => new
            {
                foodId = p.FoodID,
                promoId = p.PromoID,
                discountedPrice = p.DiscountedPrice,
                originalPrice = p.Food?.Price ?? 0m,
                discountAmount = p.Food != null ? Math.Max(0, p.Food.Price - p.DiscountedPrice) : 0m,
                discountPercent = (p.Food != null && p.Food.Price > 0)
                    ? (int)Math.Round((p.Food.Price - p.DiscountedPrice) / p.Food.Price * 100)
                    : 0
            }).ToList();

            // Compute dynamic recommendations.
            List<string> recommendedFoodIds = new();
            string dynamicLabel = "Chef's Pick";

            var userTopFoodIds = await _context.OrderItems
                .Where(oi => oi.Order.CustomerID == CurrentCustomerId && oi.FoodID != null)
                .GroupBy(oi => oi.FoodID!)
                .OrderByDescending(g => g.Sum(oi => oi.Quantity))
                .Select(g => g.Key)
                .Take(3)
                .ToListAsync();

            if (userTopFoodIds.Count >= 3)
            {
                recommendedFoodIds = userTopFoodIds;
                dynamicLabel = "Your Favorite";
            }
            else
            {
                var globalTopFoodIds = await _context.OrderItems
                    .Where(oi => oi.FoodID != null)
                    .GroupBy(oi => oi.FoodID!)
                    .OrderByDescending(g => g.Sum(oi => oi.Quantity))
                    .Select(g => g.Key)
                    .Take(3)
                    .ToListAsync();

                if (globalTopFoodIds.Count >= 3)
                {
                    recommendedFoodIds = globalTopFoodIds;
                    dynamicLabel = "Popular Choice";
                }
                else
                {
                    recommendedFoodIds = MenuItems
                        .Where(f => !string.IsNullOrEmpty(f.FoodID))
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(3)
                        .Select(f => f.FoodID!)
                        .ToList();

                    dynamicLabel = "Chef's Pick";
                }
            }

            foreach (var item in MenuItems)
            {
                if (!string.IsNullOrEmpty(item.FoodID) && recommendedFoodIds.Contains(item.FoodID))
                {
                    item.Recommendation = dynamicLabel;
                }
            }

            return Page();
        }

        // Endpoint for polling table session status in real-time
        public async Task<IActionResult> OnGetCheckTableSessionStatusAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null) return new JsonResult(new { valid = false, message = "User unauthenticated." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var activeBooking = await FindActiveBookingAsync(customer.CustomerID);
            if (activeBooking is not null)
            {
                return new JsonResult(new { valid = true, tableId = activeBooking.TableID });
            }

            var tableId = HttpContext.Session.GetString(SelectedTableSessionKey);
            var sessionOwner = HttpContext.Session.GetString(TableOwnerUserIdSessionKey);

            if (string.IsNullOrEmpty(tableId) || string.IsNullOrEmpty(userId) || !string.Equals(sessionOwner, userId, StringComparison.Ordinal))
            {
                return new JsonResult(new { valid = false, message = "Please scan the QR code at your table to start an order." });
            }

            bool isValid = await HasValidTableOwnershipAsync(tableId, userId);
            if (!isValid)
            {
                HttpContext.Session.Remove(SelectedTableSessionKey);
                HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                return new JsonResult(new { valid = false, message = TableSessionExpiredMessage });
            }

            return new JsonResult(new { valid = true, tableId = tableId });
        }

        public async Task<IActionResult> OnPostCreateOrderAsync([FromBody] CreateOrderRequest request)
        {
            if (request?.Items == null || request.Items.Count == 0)
            {
                return new JsonResult(new { success = false, message = "Cart is empty." }) { StatusCode = 400 };
            }

            if (request.PaymentMethod is not ("Stripe" or "Counter"))
            {
                return new JsonResult(new { success = false, message = "Select a valid payment method." }) { StatusCode = 400 };
            }

            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
            {
                return new JsonResult(new { success = false, message = "User not authenticated." }) { StatusCode = 401 };
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new JsonResult(new { success = false, message = "User not authenticated." }) { StatusCode = 401 };
            }

            var activeBooking = await FindActiveBookingAsync(customer.CustomerID);
            var isWalkIn = activeBooking is null;
            var tableId = activeBooking?.TableID;
            var tableLock = default(SemaphoreSlim);
            if (isWalkIn)
            {
                tableId = HttpContext.Session.GetString(SelectedTableSessionKey);
                if (string.IsNullOrWhiteSpace(tableId))
                {
                    return TableSessionError();
                }

                tableLock = _tableSessions.GetLock(tableId);
                await tableLock.WaitAsync();
            }

            try
            {
                if (activeBooking is not null && (string.IsNullOrWhiteSpace(activeBooking.TableID) || activeBooking.Table is null))
                {
                    return new JsonResult(new { success = false, message = "Your active booking's table could not be found. Please contact restaurant staff." }) { StatusCode = 409 };
                }

                if (isWalkIn && !await HasValidTableOwnershipAsync(tableId!, userId))
                {
                    HttpContext.Session.Remove(SelectedTableSessionKey);
                    HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                    return TableSessionError();
                }

                await using var transaction = await _context.Database.BeginTransactionAsync();

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

                var order = new Order
                {
                    OrderID = newOrderId,
                    CustomerID = customer.CustomerID,
                    TableID = tableId!,
                    BookingID = activeBooking?.BookingID,
                    StaffID = null,
                    OrderStatus = request.PaymentMethod == "Stripe" ? "Payment" : "Pending",
                    PaymentMethod = request.PaymentMethod,
                    CreatedAt = DateTime.Now,
                    CompletedAt = null
                };

                _context.Orders.Add(order);

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

                var latestBooking = await FindActiveBookingAsync(customer.CustomerID);
                if (latestBooking is not null)
                {
                    if (string.IsNullOrWhiteSpace(latestBooking.TableID) || latestBooking.Table is null)
                    {
                        await transaction.RollbackAsync();
                        return new JsonResult(new { success = false, message = "Your active booking's table could not be found. Please contact restaurant staff." }) { StatusCode = 409 };
                    }

                    order.TableID = latestBooking.TableID;
                    order.BookingID = latestBooking.BookingID;
                }
                else
                {
                    var currentTableId = HttpContext.Session.GetString(SelectedTableSessionKey);
                    if (!isWalkIn ||
                        !string.Equals(currentTableId, tableId, StringComparison.Ordinal) ||
                        !await HasValidTableOwnershipAsync(tableId!, userId))
                    {
                        await transaction.RollbackAsync();
                        HttpContext.Session.Remove(SelectedTableSessionKey);
                        HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                        return TableSessionError();
                    }

                    order.TableID = tableId!;
                    order.BookingID = null;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new JsonResult(new
                {
                    success = true,
                    orderId = newOrderId,
                    paymentMethod = request.PaymentMethod
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not create an order for customer {CustomerId} at table {TableId}.", customer.CustomerID, tableId);
                return new JsonResult(new { success = false, message = "The order could not be saved. Please try again." }) { StatusCode = 500 };
            }
            finally
            {
                tableLock?.Release();
            }
        }

        private async Task ResolveTableSessionAsync(string? requestedTableId, CustomerEntity customer, string? currentUserId)
        {
            if (!string.IsNullOrWhiteSpace(requestedTableId))
            {
                var candidateTable = await GetOrderableTableAsync(requestedTableId);

                if (candidateTable is null)
                {
                    HttpContext.Session.Remove(SelectedTableSessionKey);
                    HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                    TableSessionMessage = "This table QR code is invalid or does not exist. Please contact restaurant staff.";
                    return;
                }

                if (!IsWalkInOrderableTable(candidateTable))
                {
                    HttpContext.Session.Remove(SelectedTableSessionKey);
                    HttpContext.Session.Remove(TableOwnerUserIdSessionKey);

                    string currentStatus = string.IsNullOrWhiteSpace(candidateTable.TableAvailability)
                        ? "Unavailable"
                        : candidateTable.TableAvailability;

                    TableSessionMessage = $"Table {requestedTableId} is not yet activated for ordering (Current Status: {currentStatus}). Please inform restaurant staff to activate your table.";
                    return;
                }

                var customerBooking = await FindCustomerBookingForTodayAsync(customer.CustomerID, DateTime.Now);
                if (customerBooking is not null)
                {
                    if (!string.Equals(customerBooking.TableID, requestedTableId, StringComparison.OrdinalIgnoreCase))
                    {
                        TableSessionMessage = WrongTableMessage;
                        return;
                    }

                    if (!IsBookingStatusEligibleForCheckIn(customerBooking.BookingStatus))
                    {
                        TableSessionMessage = "The booking has already been claimed or is no longer eligible.";
                        return;
                    }

                    if (!IsBookingWithinCheckInWindow(customerBooking, DateTime.Now))
                    {
                        TableSessionMessage = "The booking is not within the permitted scanning time.";
                        return;
                    }
                }

                var guestBooking = await FindGuestBookingForTableAsync(requestedTableId, DateTime.Now);
                if (guestBooking is not null)
                {
                    var guestBookingFailureReason = GetGuestBookingClaimFailureReason(guestBooking, DateTime.Now);
                    if (guestBookingFailureReason is not null)
                    {
                        TableSessionMessage = guestBookingFailureReason;
                        return;
                    }

                    var claimed = await ClaimGuestBookingAsync(guestBooking, customer.CustomerID, DateTime.Now);
                    if (!claimed)
                    {
                        TableSessionMessage = "The booking has already been claimed or is no longer eligible.";
                        return;
                    }

                    _tableSessions.Claim(requestedTableId, currentUserId!);
                    HttpContext.Session.SetString(SelectedTableSessionKey, requestedTableId);
                    HttpContext.Session.SetString(TableOwnerUserIdSessionKey, currentUserId!);
                    SelectedTableId = requestedTableId;
                    CanPlaceOrder = true;
                    return;
                }

                var tableLock = _tableSessions.GetLock(requestedTableId);
                await tableLock.WaitAsync();
                try
                {
                    var table = await GetOrderableTableAsync(requestedTableId);
                    if (table is null || !IsWalkInOrderableTable(table))
                    {
                        HttpContext.Session.Remove(SelectedTableSessionKey);
                        HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                        TableSessionMessage = "This table is currently unavailable for customer ordering.";
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(currentUserId))
                    {
                        HttpContext.Session.Remove(SelectedTableSessionKey);
                        HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                        TableSessionMessage = "Please sign in again before ordering.";
                        return;
                    }

                    _tableSessions.Claim(requestedTableId, currentUserId);
                    HttpContext.Session.SetString(SelectedTableSessionKey, requestedTableId);
                    HttpContext.Session.SetString(TableOwnerUserIdSessionKey, currentUserId);
                    SelectedTableId = requestedTableId;
                    CanPlaceOrder = true;
                }
                finally
                {
                    tableLock.Release();
                }

                return;
            }

            var selectedTableId = HttpContext.Session.GetString(SelectedTableSessionKey);
            var sessionOwner = HttpContext.Session.GetString(TableOwnerUserIdSessionKey);

            if (!string.IsNullOrWhiteSpace(selectedTableId) && !string.IsNullOrWhiteSpace(currentUserId))
            {
                if (string.Equals(sessionOwner, currentUserId, StringComparison.Ordinal))
                {
                    var tableLock = _tableSessions.GetLock(selectedTableId);
                    await tableLock.WaitAsync();
                    try
                    {
                        if (await HasValidTableOwnershipAsync(selectedTableId, currentUserId))
                        {
                            SelectedTableId = selectedTableId;
                            CanPlaceOrder = true;
                            return;
                        }
                    }
                    finally
                    {
                        tableLock.Release();
                    }

                    HttpContext.Session.Remove(SelectedTableSessionKey);
                    HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
                    TableSessionMessage = TableSessionExpiredMessage;
                    return;
                }
            }

            HttpContext.Session.Remove(SelectedTableSessionKey);
            HttpContext.Session.Remove(TableOwnerUserIdSessionKey);
            TableSessionMessage = "Please scan the QR code at your table to start an order.";
        }

        private async Task<bool> HasValidTableOwnershipAsync(string tableId, string userId)
        {
            if (!string.Equals(_tableSessions.GetOwner(tableId), userId, StringComparison.Ordinal))
            {
                return false;
            }

            var table = await GetOrderableTableAsync(tableId);
            return table is not null && IsWalkInOrderableTable(table);
        }

        private async Task<Table?> GetOrderableTableAsync(string tableId)
        {
            return await _context.Tables
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.TableID == tableId);
        }

        private async Task<Booking?> FindActiveBookingAsync(string customerId)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var booking = await _context.Bookings
                .Include(booking => booking.Table)
                .AsNoTracking()
                .FirstOrDefaultAsync(booking =>
                    booking.CustomerID == customerId &&
                    booking.BookingDate == today &&
                    (booking.BookingStatus == "Confirmed" ||
                     booking.BookingStatus == "Arriving" ||
                     booking.BookingStatus == "On dine-in") &&
                    !string.IsNullOrEmpty(booking.TableID));

            if (booking is null)
            {
                return null;
            }

            if (!IsBookingWithinCheckInWindow(booking, now))
            {
                await CancelExpiredBookingIfNeededAsync(booking, now);
                return null;
            }

            return booking;
        }

        private async Task<Booking?> FindCustomerBookingForTodayAsync(string customerId, DateTime now)
        {
            var today = DateOnly.FromDateTime(now);
            return await _context.Bookings
                .Include(booking => booking.Table)
                .AsNoTracking()
                .OrderBy(booking => booking.BookingTime)
                .FirstOrDefaultAsync(booking =>
                    booking.CustomerID == customerId &&
                    booking.BookingDate == today &&
                    !string.IsNullOrEmpty(booking.TableID));
        }

        private async Task<Booking?> FindGuestBookingForTableAsync(string tableId, DateTime now)
        {
            var today = DateOnly.FromDateTime(now);
            return await _context.Bookings
                .Include(booking => booking.Table)
                .AsNoTracking()
                .OrderBy(booking => booking.BookingTime)
                .FirstOrDefaultAsync(booking =>
                    booking.CustomerID == null &&
                    booking.TableID == tableId &&
                    booking.BookingDate == today &&
                    (booking.BookingStatus == "Confirmed" ||
                     booking.BookingStatus == "Arriving" ||
                     booking.BookingStatus == "On dine-in"));
        }

        private async Task<bool> ClaimGuestBookingAsync(Booking guestBooking, string customerId, DateTime now)
        {
            if (!IsBookingWithinCheckInWindow(guestBooking, now))
            {
                return false;
            }

            var affectedRows = await _context.Bookings
                .Where(booking =>
                    booking.BookingID == guestBooking.BookingID &&
                    booking.CustomerID == null &&
                    booking.TableID == guestBooking.TableID &&
                    booking.BookingDate == guestBooking.BookingDate &&
                    booking.BookingTime == guestBooking.BookingTime &&
                    (booking.BookingStatus == "Confirmed" ||
                     booking.BookingStatus == "Arriving" ||
                     booking.BookingStatus == "On dine-in"))
                .ExecuteUpdateAsync(update => update.SetProperty(booking => booking.CustomerID, customerId));

            return affectedRows == 1;
        }

        private async Task CancelExpiredBookingIfNeededAsync(Booking booking, DateTime now)
        {
            if (booking.BookingDate > DateOnly.FromDateTime(now)) return;

            // Do not auto-cancel if customer is already dining
            if (booking.BookingStatus == "On dine-in") return;

            if (!IsBookingStatusEligibleForCheckIn(booking.BookingStatus)) return;

            var bookingDateTime = booking.BookingDate.ToDateTime(booking.BookingTime, DateTimeKind.Local);
            if (now < bookingDateTime.AddMinutes(30)) return;

            // Direct database update or re-attach entity
            var dbBooking = await _context.Bookings.FindAsync(booking.BookingID);
            if (dbBooking != null && dbBooking.BookingStatus != "Cancelled")
            {
                dbBooking.BookingStatus = "Cancelled";
                await ReservationStatusWorkflow.ApplyAsync(_context, dbBooking, "Cancelled");
                await _context.SaveChangesAsync();
            }
        }

        private static string? GetGuestBookingClaimFailureReason(Booking booking, DateTime now)
        {
            if (booking.CustomerID != null)
            {
                return "The booking has already been claimed or is no longer eligible.";
            }

            if (!IsBookingWithinCheckInWindow(booking, now))
            {
                return "The booking is not within the permitted scanning time.";
            }

            if (!IsBookingStatusEligibleForCheckIn(booking.BookingStatus))
            {
                return "The booking has already been claimed or is no longer eligible.";
            }

            if (booking.Table is null || !IsWalkInOrderableTable(booking.Table))
            {
                return "The assigned table is not eligible for ordering at the moment.";
            }

            return null;
        }

        private static bool IsBookingStatusEligibleForCheckIn(string? bookingStatus) =>
            bookingStatus is "Confirmed" or "Arriving" or "On dine-in";

        private static bool IsBookingWithinCheckInWindow(Booking booking, DateTime now)
        {
            if (booking.BookingDate == default || booking.BookingTime == default)
            {
                return false;
            }

            var bookingDateTime = booking.BookingDate.ToDateTime(booking.BookingTime, DateTimeKind.Local);
            var earliestAllowed = bookingDateTime.AddMinutes(-30);
            var latestAllowed = bookingDateTime.AddMinutes(30);
            return now >= earliestAllowed && now <= latestAllowed;
        }

        private static bool IsWalkInOrderableTable(Table table) =>
            table.TableAvailability.Equals("On dine-in", StringComparison.OrdinalIgnoreCase);

        private JsonResult TableSessionError() =>
            new(new { success = false, message = TableSessionExpiredMessage }) { StatusCode = 409 };

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
        public List<CartItemDto> Items { get; set; } = new();
        public string PaymentMethod { get; set; } = string.Empty;
    }

    public class CartItemDto
    {
        public string FoodId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string? Remarks { get; set; }
    }
}

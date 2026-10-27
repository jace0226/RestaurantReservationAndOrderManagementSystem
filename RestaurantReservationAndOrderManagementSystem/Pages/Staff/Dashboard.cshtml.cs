using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;

        public DashboardModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public string StaffName =>
            User.FindFirstValue(ClaimTypes.Name)?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? "team";

        public string Greeting => DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };

        public int ActiveOrderCount { get; private set; }
        public int ReadyOrderCount { get; private set; }
        public int TodayReservationCount { get; private set; }
        public int AvailableTableCount { get; private set; }
        public int TotalTableCount { get; private set; }
        public int GuestsDiningIn { get; private set; }
        public IReadOnlyList<OrderRow> ActiveOrders { get; private set; } = [];
        public IReadOnlyList<ReservationRow> UpcomingReservations { get; private set; } = [];
        public IReadOnlyList<TableRow> TablesPreview { get; private set; } = [];

        public async Task OnGetAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var now = TimeOnly.FromDateTime(DateTime.Now);
            var activeStatuses = new[] { "Completed", "Cancelled" };

            ActiveOrderCount = await _dbContext.Orders.CountAsync(order =>
                !activeStatuses.Contains(order.OrderStatus));
            ReadyOrderCount = await _dbContext.Orders.CountAsync(order =>
                order.OrderStatus == "Ready" || order.OrderStatus == "Ready to Serve");

            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Where(order => !activeStatuses.Contains(order.OrderStatus))
                .Include(order => order.Customer)
                    .ThenInclude(customer => customer.User)
                .Include(order => order.Table)
                .OrderByDescending(order => order.CreatedAt)
                .Take(3)
                .ToListAsync();
            var orderIds = orders.Select(order => order.OrderID).ToArray();
            var itemCounts = orderIds.Length == 0
                ? new Dictionary<string, int>()
                : await _dbContext.OrderItems
                    .Where(item => orderIds.Contains(item.OrderID))
                    .GroupBy(item => item.OrderID)
                    .Select(group => new
                    {
                        OrderId = group.Key,
                        Quantity = group.Sum(item => item.Quantity)
                    })
                    .ToDictionaryAsync(group => group.OrderId, group => group.Quantity);
            ActiveOrders = orders.Select(order => new OrderRow(
                order.OrderID,
                order.Customer.User.FullName,
                order.Table.TableID,
                itemCounts.GetValueOrDefault(order.OrderID),
                order.OrderStatus)).ToList();

            TodayReservationCount = await _dbContext.Bookings.CountAsync(booking =>
                booking.BookingDate == today);

            var reservations = await _dbContext.Bookings
                .AsNoTracking()
                .Where(booking => booking.BookingDate == today)
                .Include(booking => booking.Customer)
                    .ThenInclude(customer => customer.User)
                .Include(booking => booking.Table)
                .OrderBy(booking => booking.BookingTime)
                .ToListAsync();
            UpcomingReservations = reservations
            .Where(booking => booking.BookingTime >= now)
            .Take(3)
            .Select(booking => new ReservationRow(
                booking.BookingTime,
                booking.Customer?.User?.FullName ?? "Guest",
                booking.PartySize,
                booking.Table?.TableID ?? "Unassigned",
                booking.BookingStatus ?? ""))
            .ToList();
            //UpcomingReservations = reservations
            //    .Where(booking => booking.BookingTime >= now)
            //    .Take(3)
            //    .Select(booking => new ReservationRow(
            //        booking.BookingTime,
            //        booking.Customer.User.FullName,
            //        booking.PartySize,
            //        booking.Table.TableID,
            //        booking.BookingStatus))
            //    .ToList();

            var tables = await _dbContext.Tables
                .AsNoTracking()
                .OrderBy(table => table.TableID)
                .ToListAsync();
            TotalTableCount = tables.Count;
            AvailableTableCount = tables.Count(table =>
                table.TableAvailability.Equals("Available", StringComparison.OrdinalIgnoreCase));
            TablesPreview = tables.Take(6)
                .Select(table => new TableRow(
                    table.TableID,
                    table.MaxPax,
                    table.TableAvailability))
                .ToList();

            GuestsDiningIn = await _dbContext.Bookings
                .Where(booking =>
                    booking.BookingDate == today &&
                    booking.BookingStatus == "On dine-in")
                .SumAsync(booking => (int?)booking.PartySize) ?? 0;
        }

        public static string OrderStatusCssClass(string status) => status.ToLowerInvariant() switch
        {
            "preparing" => "status-preparing",
            "ready" or "ready to serve" => "status-ready",
            _ => "status-pending"
        };

        public static string TableStatusCssClass(string status) => status.ToLowerInvariant() switch
        {
            "available" => "table-free",
            "reserved" => "table-reserved",
            _ => "table-busy"
        };

        public sealed record OrderRow(
            string OrderId,
            string CustomerName,
            string TableId,
            int ItemCount,
            string Status);

        public sealed record ReservationRow(
            TimeOnly Time,
            string CustomerName,
            int PartySize,
            string TableId,
            string Status);

        public sealed record TableRow(string TableId, int Capacity, string Status);
    }
}

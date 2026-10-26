using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Staff
{
    public class ReportsModel : PageModel
    {
        public List<(DateOnly Start, DateOnly End)> Weeks { get; set; } = new();
        public List<string> RevenueLabels { get; set; } = new();
        public List<decimal> RevenueValues { get; set; } = new();
        public decimal TotalRevenue { get; set; } = 0m;
        public decimal PreviousWeekRevenue { get; set; } = 0m;
        public decimal? RevenueChangePercentage { get; set; }
        public int TotalOrders { get; set; } = 0;
        public int PreviousWeekOrders { get; set; } = 0;
        public decimal? OrdersChangePercentage { get; set; }
        public int TotalBooking { get; set; } = 0;
        public int PreviousWeekBookings { get; set; } = 0;
        public decimal? BookingsChangePercentage { get; set; }

        public class PopularFoodItem {
            public string FoodName { get; set; } = string.Empty;
            public int QuantitySold { get; set; }
            public decimal TotalSales { get; set; }
        }

        public class PaymentSummary {
            public string PaymentMethod { get; set; } = string.Empty;
            public int NumberOfTransaction { get; set; }
            public decimal TransactionAmount { get; set; }
        }

        public List<PopularFoodItem> PopularFoodItems { get; set; } = new();
        public List<PaymentSummary> PaymentSummaries { get; set; } = new();

        private readonly ApplicationDbContext _context;

        public ReportsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public DateOnly? WeekStart { get; set; }

        public DateOnly SelectedWeekStart { get; set; }
        public DateOnly SelectedWeekEnd { get; set; }

        public async Task OnGet() {
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);

            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            DateOnly currentMonday = today.AddDays(-daysSinceMonday);

            for (int i = 0; i < 12; i++) {
                DateOnly start = currentMonday.AddDays(-7 * i);
                DateOnly end = start.AddDays(6);

                Weeks.Add((start, end));
            }

            SelectedWeekStart = Weeks.Any(w => w.Start == WeekStart) ? WeekStart!.Value : currentMonday;

            SelectedWeekEnd = SelectedWeekStart.AddDays(6);

            var transactions = await _context.Transactions.Where(t => t.TransactionDate >= SelectedWeekStart && t.TransactionDate <= SelectedWeekEnd).ToListAsync();

            RevenueLabels = Enumerable.Range(0, 7).Select(i => SelectedWeekStart.AddDays(i).ToString("ddd")).ToList();

            RevenueValues = Enumerable.Range(0, 7).Select(i =>
            {
                DateOnly date = SelectedWeekStart.AddDays(i);

                return transactions.Where(t => t.TransactionDate == date).Sum(t => t.TransactionAmount);
            }).ToList();

            DateOnly previousWeekStart = SelectedWeekStart.AddDays(-7);
            DateOnly previousWeekEnd = SelectedWeekEnd.AddDays(-7);

            TotalRevenue = RevenueValues.Sum();
            var previousTransactions = await _context.Transactions.Where(t => t.TransactionDate >= previousWeekStart && t.TransactionDate <= previousWeekEnd).ToListAsync();

            PreviousWeekRevenue = previousTransactions.Sum(t => t.TransactionAmount);

            if (PreviousWeekRevenue > 0)
            {
                decimal change = ((TotalRevenue - PreviousWeekRevenue) / PreviousWeekRevenue) * 100m;

                RevenueChangePercentage = change == 0m ? null : change;
            }
            else 
            {
                RevenueChangePercentage = null;
            }

            DateTime startDate = SelectedWeekStart.ToDateTime(TimeOnly.MinValue);
            DateTime endDate = SelectedWeekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

            DateTime previousStartDate = previousWeekStart.ToDateTime(TimeOnly.MinValue);
            DateTime previousEndDate = previousWeekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

            TotalOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= startDate && o.CreatedAt < endDate);

            PreviousWeekOrders = await _context.Orders.CountAsync(o => o.CreatedAt >= previousStartDate && o.CreatedAt < previousEndDate);
            if (PreviousWeekOrders > 0)
            {
                decimal change = ((TotalOrders - PreviousWeekOrders) / PreviousWeekOrders) * 100m;

                OrdersChangePercentage = change == 0m ? null : change;
            }
            else
            {
                OrdersChangePercentage = null;
            }

            TotalBooking = await _context.Bookings.CountAsync(b => b.BookingDate >= SelectedWeekStart && b.BookingDate <= SelectedWeekEnd);

            PreviousWeekBookings = await _context.Bookings.CountAsync(b => b.BookingDate >= previousWeekStart && b.BookingDate <= previousWeekEnd);
            if (PreviousWeekBookings > 0)
            {
                decimal change = ((TotalBooking - PreviousWeekBookings) / PreviousWeekBookings) * 100m;

                BookingsChangePercentage = change == 0m ? null : change;
            }
            else
            {
                BookingsChangePercentage = null;
            }

            PopularFoodItems = await _context.OrderItems.Where(oi => oi.Order.CreatedAt >= startDate && oi.Order.CreatedAt < endDate).GroupBy(oi => new
            {
                oi.FoodID,
                oi.Food.FoodName
            }).Select(g => new PopularFoodItem
            {
                FoodName = g.Key.FoodName,
                QuantitySold = g.Sum(oi => oi.Quantity),
                TotalSales = g.Sum(oi => oi.Quantity * oi.Food.Price)
            }).OrderByDescending(item => item.QuantitySold).Take(5).ToListAsync();

            PaymentSummaries = await _context.Transactions.Where(t => t.TransactionDate >= previousWeekStart && t.TransactionDate <= previousWeekEnd).GroupBy(t =>
                t.TransactionType
            ).Select(g => new PaymentSummary
            {
                PaymentMethod = g.Key,
                NumberOfTransaction = g.Count(),
                TransactionAmount = g.Sum(t => t.TransactionAmount)
            }).OrderByDescending(p => p.TransactionAmount).ToListAsync();
        }
    }
}

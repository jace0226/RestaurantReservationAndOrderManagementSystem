using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;

namespace RestaurantReservationAndOrderManagementSystem.Services
{
    public class BookingExpirationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingExpirationWorker> _logger;

        public BookingExpirationWorker(
            IServiceProvider serviceProvider,
            ILogger<BookingExpirationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Booking Expiration Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var now = DateTime.Now;
                        var today = DateOnly.FromDateTime(now);

                        // Query active bookings for today that might be past the 30-min window
                        var candidates = await context.Bookings
                            .Where(b => b.BookingDate == today &&
                                       (b.BookingStatus == "Confirmed" || b.BookingStatus == "Arriving"))
                            .ToListAsync(stoppingToken);

                        bool changed = false;
                        foreach (var booking in candidates)
                        {
                            var bookingTime = booking.BookingDate.ToDateTime(booking.BookingTime, DateTimeKind.Local);

                            // Auto-cancel if 30 minutes past booking start time
                            if (now >= bookingTime.AddMinutes(30))
                            {
                                booking.BookingStatus = "Cancelled";
                                await ReservationStatusWorkflow.ApplyAsync(context, booking, "Cancelled");
                                changed = true;
                                _logger.LogInformation("Auto-cancelled expired booking {BookingID}", booking.BookingID);
                            }
                        }

                        if (changed)
                        {
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired bookings.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
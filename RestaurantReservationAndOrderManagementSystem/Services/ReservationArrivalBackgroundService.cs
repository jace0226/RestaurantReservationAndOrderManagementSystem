using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;

namespace RestaurantReservationAndOrderManagementSystem.Services;

public sealed class ReservationArrivalBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationArrivalBackgroundService> _logger;

    public ReservationArrivalBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationArrivalBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MarkArrivingReservationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not process upcoming reservation arrivals.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task MarkArrivingReservationsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
        var arrivalCutoff = now.AddHours(1);
        var cutoffDate = DateOnly.FromDateTime(arrivalCutoff);
        var cutoffTime = TimeOnly.FromDateTime(arrivalCutoff);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var upcomingBookings = await dbContext.Bookings
            .Where(booking =>
                booking.BookingStatus == "Confirmed" &&
                (booking.BookingDate > today ||
                 (booking.BookingDate == today && booking.BookingTime > currentTime)) &&
                (booking.BookingDate < cutoffDate ||
                 (booking.BookingDate == cutoffDate && booking.BookingTime <= cutoffTime)))
            .ToListAsync(cancellationToken);

        if (upcomingBookings.Count == 0)
        {
            return;
        }

        foreach (var booking in upcomingBookings)
        {
            await ReservationStatusWorkflow.ApplyAsync(
                dbContext,
                booking,
                "Arriving",
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Marked {ReservationCount} upcoming reservation(s) as arriving.",
            upcomingBookings.Count);
    }
}

using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Services;

public static class ReservationStatusWorkflow
{
    public static async Task ApplyAsync(
        ApplicationDbContext dbContext,
        Booking booking,
        string status,
        CancellationToken cancellationToken = default)
    {
        booking.BookingStatus = status;

        if (string.IsNullOrWhiteSpace(booking.TableID))
        {
            return;
        }

        var table = await dbContext.Tables
            .SingleOrDefaultAsync(item => item.TableID == booking.TableID, cancellationToken);
        if (table is null)
        {
            return;
        }

        switch (status)
        {
            case "Pending":
                if (table.TableAvailability.Equals("Reserved", StringComparison.OrdinalIgnoreCase) ||
                    table.TableAvailability.Equals("On dine-in", StringComparison.OrdinalIgnoreCase) ||
                    table.TableAvailability.Equals("Occupied", StringComparison.OrdinalIgnoreCase))
                {
                    table.TableAvailability = "Available";
                }

                break;
            case "Confirmed":
                table.TableAvailability = "Available";
                break;
            case "Arriving":
                table.TableAvailability = "Reserved";
                break;
            case "On dine-in":
                table.TableAvailability = "On dine-in";
                break;
            case "Completed":
                table.TableAvailability = "Available";
                break;
            case "Cancelled":
                if (table.TableAvailability.Equals("Reserved", StringComparison.OrdinalIgnoreCase) ||
                    table.TableAvailability.Equals("On dine-in", StringComparison.OrdinalIgnoreCase) ||
                    table.TableAvailability.Equals("Occupied", StringComparison.OrdinalIgnoreCase))
                {
                    table.TableAvailability = "Available";
                }

                break;
        }
    }
}

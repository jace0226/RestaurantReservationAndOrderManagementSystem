using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Booking
    {
        [Key]
        public string BookingID { get; set; }

        public string CustomerID { get; set; }

        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; } = null!;

        public string TableID { get; set; }

        [ForeignKey(nameof(TableID))]
        public Table Table { get; set; } = null!;

        public DateOnly BookingDate { get; set; }

        public TimeOnly BookingTime { get; set; }

        public string BookingStatus { get; set; }

        public string BookingNotes { get; set; }
    }
}

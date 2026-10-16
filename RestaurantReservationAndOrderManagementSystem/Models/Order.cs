using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Order
    {
        [Key]
        public string OrderID { get; set; }

        public string CustomerID { get; set; }

        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; } = null!;

        public string StaffID { get; set; }

        [ForeignKey(nameof(StaffID))]
        public Staff Staff { get; set; } = null!;

        public string BookingID { get; set; }

        [ForeignKey(nameof(BookingID))]
        public Booking Booking { get; set; } = null!;

        public string TableID { get; set; }

        [ForeignKey(nameof(TableID))]
        public Table Table { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime CompletedAt { get; set; }

        public string OrderStatus { get; set; }

        public string OrderNotes { get; set; }
    }
}

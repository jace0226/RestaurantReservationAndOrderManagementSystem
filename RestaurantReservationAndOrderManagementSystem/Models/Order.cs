using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Order
    {
        [Key]
        [Required(ErrorMessage = "Order ID is required")]
        public string OrderID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer ID is required")]
        public string CustomerID { get; set; } = string.Empty;

        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; } = null!;

        public string? StaffID { get; set; }

        [ForeignKey(nameof(StaffID))]
        public Staff? Staff { get; set; }

        public string? BookingID { get; set; }

        [ForeignKey(nameof(BookingID))]
        public Booking? Booking { get; set; }

        [Required(ErrorMessage = "Table ID is required")]
        public string TableID { get; set; } = string.Empty;

        [ForeignKey(nameof(TableID))]
        public Table Table { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? CompletedAt { get; set; }

        public string OrderStatus { get; set; } = string.Empty;
    }
}

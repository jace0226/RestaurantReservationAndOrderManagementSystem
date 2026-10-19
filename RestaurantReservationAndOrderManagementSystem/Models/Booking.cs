using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Booking
    {
        [Key]
        [Required(ErrorMessage = "Booking ID is required")]
        public string BookingID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer ID is required")]
        public string CustomerID { get; set; } = string.Empty;

        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; } = null!;

        [Required(ErrorMessage = "Table ID is required")]
        public string TableID { get; set; } = string.Empty;

        [ForeignKey(nameof(TableID))]
        public Table Table { get; set; } = null!;

        [Required(ErrorMessage = "Booking Date is required")]
        public DateOnly BookingDate { get; set; } = DateOnly.MinValue;

        [Required(ErrorMessage = "Booking Time is required")]
        public TimeOnly BookingTime { get; set; } = TimeOnly.MinValue;

        public string BookingStatus { get; set; } = string.Empty;

        public string BookingNotes { get; set; } = string.Empty;
    }
}

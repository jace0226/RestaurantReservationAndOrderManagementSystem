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

        public string? TableID { get; set; }

        [ForeignKey(nameof(TableID))]
        public Table? Table { get; set; }

        [Required(ErrorMessage = "Booking Date is required")]
        public DateOnly BookingDate { get; set; } = DateOnly.MinValue;

        [Required(ErrorMessage = "Booking Time is required")]
        public TimeOnly BookingTime { get; set; } = TimeOnly.MinValue;

        [Range(1, 100)]
        public int PartySize { get; set; } = 1;

        public string BookingStatus { get; set; } = string.Empty;

        public string BookingNotes { get; set; } = string.Empty;
    }
}

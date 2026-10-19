using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Feedback
    {
        [Key]
        [Required(ErrorMessage = "Feedback ID is required")]
        public string FeedbackID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer ID is required")]
        public string CustomerID { get; set; } = string.Empty;

        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; } = null!;

        public int Stars { get; set; } = 0;

        public string Message { get; set; } = string.Empty;
    }
}

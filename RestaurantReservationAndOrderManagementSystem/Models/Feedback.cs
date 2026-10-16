using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Feedback
    {
        [Key]
        public string FeedbackID { get; set; }

        public string CustomerID { get; set; }

        [ForeignKey(nameof(CustomerID))]
        public Customer Customer { get; set; } = null!;

        public int Stars { get; set; }

        public string Message { get; set; }
    }
}

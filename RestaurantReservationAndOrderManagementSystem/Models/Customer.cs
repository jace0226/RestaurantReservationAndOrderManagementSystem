using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Customer
    {
        [Key]
        [Required(ErrorMessage = "Customer ID is required")]
        public string CustomerID { get; set; } = string.Empty;

        [Required(ErrorMessage = "User ID is required")]
        public string UserID { get; set; } = string.Empty;

        [ForeignKey(nameof(UserID))]
        public User User { get; set; } = null!;

        public DateOnly DateOfBirth { get; set; } = DateOnly.MinValue;

        public string Address { get; set; } = string.Empty;
    }
}

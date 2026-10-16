using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Customer
    {
        [Key]
        public string CustomerID { get; set; }

        public int UserID { get; set; }

        [ForeignKey(nameof(UserID))]
        public User User { get; set; } = null!;

        public DateOnly DateOfBirth { get; set; }

        public string Address { get; set; }
    }
}

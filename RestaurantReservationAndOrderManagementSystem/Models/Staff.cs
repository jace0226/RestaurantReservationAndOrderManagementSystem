using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Staff
    {
        [Key]
        public string StaffID { get; set; }

        public int UserID { get; set; }

        [ForeignKey(nameof(UserID))]
        public User User { get; set; } = null!;

        public Roles Role { get; set; }

    }

    public enum Roles { 
        Staff,
        Admin
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Staff
    {
        [Key]
        [Required(ErrorMessage = "Staff ID is required")]
        public string StaffID { get; set; } = string.Empty;

        [Required(ErrorMessage = "User ID is required")]
        public string UserID { get; set; } = string.Empty;

        [ForeignKey(nameof(UserID))]
        public User User { get; set; } = null!;

        [Required(ErrorMessage = "Role is required")]
        public Roles Role { get; set; } = Roles.Staff;

    }

    public enum Roles { 
        Staff,
        Admin
    }
}

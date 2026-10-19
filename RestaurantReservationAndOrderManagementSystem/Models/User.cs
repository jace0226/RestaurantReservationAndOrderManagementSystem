using System.ComponentModel.DataAnnotations;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class User
    {
        [Key]
        public string UserID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full Name is required.")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(32)]
        public string Phone { get; set; } = string.Empty;
    }
}

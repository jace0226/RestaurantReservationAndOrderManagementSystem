using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Table
    {
        [Key]
        [Required(ErrorMessage = "Table ID is required")]
        public string TableID { get; set; } = string.Empty;
        
        [Range(1, 100, ErrorMessage = "Capacity must be between 1 and 100.")]
        public int MaxPax { get; set; } = 0;

        [Required(ErrorMessage = "Table Availability is required")]
        public string TableAvailability { get; set; } = string.Empty;
    }
}

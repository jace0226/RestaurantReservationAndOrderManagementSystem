using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Food_Items
    {
        [Key]
        [Required(ErrorMessage = "Food ID is required")]
        public string FoodID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Staff ID is required")]
        public string StaffID { get; set; } = string.Empty;

        [ForeignKey(nameof(StaffID))]
        public Staff Staff { get; set; } = null!;

        [Required(ErrorMessage = "Food Name is required")]
        public string FoodName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        public decimal Price { get; set; } = 0.0m;

        public string FoodDescription { get; set; } = string.Empty;

        public string FullDescription { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string FoodAvailability { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string ImageAlt { get; set; } = string.Empty;

        public string PrepTime { get; set; } = string.Empty;

        public string Dietary { get; set; } = string.Empty;

    }
}
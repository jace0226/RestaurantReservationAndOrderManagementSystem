using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Promotion
    {
        [Key]
        [Required(ErrorMessage = "Promo ID is required")]
        public string PromoID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Food ID is required")]
        public string FoodID { get; set; } = string.Empty;

        [ForeignKey(nameof(FoodID))]
        public Food_Items Food { get; set; } = null!;

        [Required(ErrorMessage = "Staff ID is required")]
        public string StaffID { get; set; } = string.Empty;

        [ForeignKey(nameof(StaffID))]
        public Staff Staff { get; set; } = null!;

        public decimal DiscountedPrice { get; set; } = 0.0m;

        public DateOnly DurationStart { get; set; } = DateOnly.MinValue;

        public TimeOnly DurationEnd { get; set; } = TimeOnly.MinValue;
    }
}

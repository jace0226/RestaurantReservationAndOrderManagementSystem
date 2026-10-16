using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Promotion
    {
        [Key]
        public string PromoID { get; set; }

        public string FoodID { get; set; }

        [ForeignKey(nameof(FoodID))]
        public Food_Items Food { get; set; } = null!;

        public string StaffID { get; set; }

        [ForeignKey(nameof(StaffID))]
        public Staff Staff { get; set; } = null!;

        public decimal DiscountedPrice { get; set; }

        public DateOnly DurationStart { get; set; }

        public TimeOnly DurationEnd { get; set; }
    }
}

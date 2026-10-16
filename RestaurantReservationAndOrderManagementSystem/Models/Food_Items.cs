using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Food_Items
    {
        [Key]
        public string FoodID { get; set; }

        public string StaffID { get; set; }

        [ForeignKey(nameof(StaffID))]
        public Staff Staff { get; set; } = null!;

        public string FoodName { get; set; }

        public decimal Price { get; set; }

        public string FoodDescription { get; set; }

        public string FoodAvailability { get; set; }
    }
}

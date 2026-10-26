using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Order_Items
    {
        [Key]
        [Required(ErrorMessage = "Order Item ID is required")]
        public string OrderItemID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Order ID is required")]
        public string OrderID { get; set; } = string.Empty;

        [ForeignKey(nameof(OrderID))]
        public Order Order { get; set; } = null!;

        [Required(ErrorMessage = "Food ID is required")]
        public required string FoodID { get; set; }

        [ForeignKey(nameof(FoodID))]
        public Food_Items Food { get; set; } = null!;

        public int Quantity { get; set; } = 0;

        public string? FoodRemarks { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Order_Items
    {
        [Key]
        public string OrderItemID { get; set; }

        public string OrderID { get; set; }

        [ForeignKey(nameof(OrderID))]
        public Order Order { get; set; } = null!;

        public string FoodID { get; set; }

        [ForeignKey(nameof(FoodID))]
        public Food_Items Food { get; set; } = null!;

        public int Quantity { get; set; }
    }
}

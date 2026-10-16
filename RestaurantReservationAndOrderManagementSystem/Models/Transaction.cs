using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Transaction
    {
        [Key]
        public string TransactionID { get; set; }

        public string OrderID { get; set; }

        [ForeignKey(nameof(OrderID))]
        public Order Order { get; set; } = null!;

        public string TransactionType { get; set; }

        public DateOnly TransactionDate { get; set; } 

        public TimeOnly TransactionTime { get; set; }

        public decimal TransactionAmount { get; set; }
    }
}

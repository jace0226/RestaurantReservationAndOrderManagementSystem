using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Transaction
    {
        [Key]
        [Required(ErrorMessage = "Transaction ID is required")]
        public string TransactionID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Order ID is required")]
        public string OrderID { get; set; } = string.Empty;

        [ForeignKey(nameof(OrderID))]
        public Order Order { get; set; } = null!;

        [Required(ErrorMessage = "Transaction Type is required")]
        public string TransactionType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Transaction Date is required")]
        public DateOnly TransactionDate { get; set; } = DateOnly.MinValue;

        [Required(ErrorMessage = "Transaction Time is required")]
        public TimeOnly TransactionTime { get; set; } = TimeOnly.MinValue;

        [Required(ErrorMessage = "Transaction Amount is required")]
        public decimal TransactionAmount { get; set; } = 0.0m;
    }
}

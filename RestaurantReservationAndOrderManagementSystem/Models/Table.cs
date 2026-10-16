using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantReservationAndOrderManagementSystem.Models
{
    public class Table
    {
        [Key]
        public string TableID { get; set; }

        public int MaxPax { get; set; }

        public string TableAvailability { get; set; }
    }
}

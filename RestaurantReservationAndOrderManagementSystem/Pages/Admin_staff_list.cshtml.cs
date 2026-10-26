using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class Admin_staff_listModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public Admin_staff_listModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public class StaffViewModel { 
            public string StaffID { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public Roles Role { get; set; }
        }

        public class CustomerViewModel { 
            public string CustomerID { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public DateOnly DateOfBirth { get; set; }
            public string Address { get; set; }
        }
        public List<StaffViewModel> Staffs { get; set; } = new();
        public List<CustomerViewModel> Customers { get; set; } = new();

        public async Task OnGetAsync() {
            Staffs = await (
                from staff in _context.Staffs
                join user in _context.Users
                on staff.UserID equals user.UserID
                select new StaffViewModel { 
                    StaffID = staff.StaffID,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = staff.Role,
                }
                ).ToListAsync();

            Customers = await (
                from customer in _context.Customers
                join user in _context.Users
                on customer.UserID equals user.UserID
                select new CustomerViewModel
                {
                    CustomerID = customer.CustomerID,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    DateOfBirth = customer.DateOfBirth,
                    Address = customer.Address,
                }
                ).ToListAsync();
        }
    }
}

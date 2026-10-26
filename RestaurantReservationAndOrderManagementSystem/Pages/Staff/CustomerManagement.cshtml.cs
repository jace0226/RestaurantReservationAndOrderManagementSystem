using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using RestaurantReservationAndOrderManagementSystem.Services;
using System.Net.Mail;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Staff
{
    public class CustomerManagementModel : PageModel
    {
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        private const string TemporaryPassword = "Bites123";

        public CustomerManagementModel(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            IEmailService emailService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        public class CustomerViewModel
        {
            public string CustomerID { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public DateOnly DateOfBirth { get; set; } = DateOnly.MinValue;
            public string Address { get; set; } = string.Empty;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<CustomerViewModel> Customers { get; set; } = new();

        // Fields submitted by the Add/Edit forms.
        [BindProperty]
        public string CustomerID { get; set; } = string.Empty;

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Phone { get; set; } = string.Empty;

        [BindProperty]
        public DateOnly DateOfBirth { get; set; } = DateOnly.MinValue;

        [BindProperty]
        public string Address { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            await LoadCustomerAsync();
        }

        // Generate IDs such as C001, C002, U001, U002.
        private async Task<string> GenerateNextIdAsync(
            IQueryable<string> existingIds,
            string prefix)
        {
            var ids = await existingIds.ToListAsync();

            int maxNumber = ids
                .Where(id =>
                    !string.IsNullOrWhiteSpace(id) &&
                    id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(id =>
                {
                    string numberPart = id.Substring(prefix.Length);

                    return int.TryParse(numberPart, out int number)
                        ? number
                        : 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            return $"{prefix}{maxNumber + 1:D3}";
        }

        // Load customer and related user information.
        private async Task LoadCustomerAsync()
        {
            var query =
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
                    Address = customer.Address
                };

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                string term = SearchTerm.Trim();

                query = query.Where(c =>
                    c.CustomerID.Contains(term) ||
                    c.FullName.Contains(term) ||
                    c.Email.Contains(term) ||
                    c.Phone.Contains(term) ||
                    c.Address.Contains(term));
            }

            Customers = await query
                .OrderBy(c => c.CustomerID)
                .ToListAsync();
        }

        // POST: Add a new customer.
        public async Task<IActionResult> OnPostAddUserAsync()
        {
            // 1. Validate required fields.
            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Phone))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Please fill in all fields.");

                await LoadCustomerAsync();
                return Page();
            }

            if (DateOfBirth == DateOnly.MinValue || DateOfBirth > DateOnly.FromDateTime(DateTime.Today)){
                ModelState.AddModelError(nameof(DateOfBirth), "Please enter a valid date of birth.");

                await LoadCustomerAsync();
                return Page();
            }

            string normalizedEmail = Email.Trim();

            // 2. Validate email format.
            try
            {
                var emailAddress = new MailAddress(normalizedEmail);

                if (emailAddress.Address != normalizedEmail)
                {
                    ModelState.AddModelError(
                        nameof(Email),
                        "Please enter a valid email address.");

                    await LoadCustomerAsync();
                    return Page();
                }
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Please enter a valid email address.");

                await LoadCustomerAsync();
                return Page();
            }

            // 4. Prevent duplicate email addresses.
            bool emailExists = await _context.Users.AnyAsync(
                u => u.Email == normalizedEmail);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "This email address is already in use.");

                await LoadCustomerAsync();
                return Page();
            }

            // 5. Generate the next CustomerID and UserID.
            string newCustomerID = await GenerateNextIdAsync(
                _context.Customers.Select(c => c.CustomerID),
                "C");

            string newUserID = await GenerateNextIdAsync(
                _context.Users
                    .Where(u => u.UserID.StartsWith("U"))
                    .Select(u => u.UserID),
                "U");

            // 6. Create the user account.
            var user = new User
            {
                UserID = newUserID,
                FullName = FullName.Trim(),
                Email = normalizedEmail,
                Phone = Phone.Trim()
            };

            // Store a password hash, never the plain-text password.
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                TemporaryPassword);

            // 7. Create the customer record linked to the user.
            var customer = new Customer
            {
                CustomerID = newCustomerID,
                UserID = newUserID,
                DateOfBirth = DateOfBirth,
                Address = Address?.Trim() ?? string.Empty,
                User = user
            };

            _context.Users.Add(user);
            _context.Customers.Add(customer);

            // 8. Save both records.
            await _context.SaveChangesAsync();

            string subject = "Account Creation";
            string body = "Your account has been created.";

            await _emailService.SendEmailAsync(normalizedEmail, subject, body);

            // 9. Return to the customer list.
            return RedirectToPage();
        }

        // POST: Edit an existing customer.
        public async Task<IActionResult> OnPostEditUserAsync()
        {
            // 1. Validate required fields.
            if (string.IsNullOrWhiteSpace(CustomerID) ||
                string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Phone))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Please fill in all fields.");

                await LoadCustomerAsync();
                return Page();
            }

            if (DateOfBirth == DateOnly.MinValue || DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError(nameof(DateOfBirth), "Please enter a valid date of birth.");

                await LoadCustomerAsync();
                return Page();
            }

            string normalizedEmail = Email.Trim();

            // 2. Validate email format.
            try
            {
                var emailAddress = new MailAddress(normalizedEmail);

                if (emailAddress.Address != normalizedEmail)
                {
                    ModelState.AddModelError(
                        nameof(Email),
                        "Please enter a valid email address.");

                    await LoadCustomerAsync();
                    return Page();
                }
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Please enter a valid email address.");

                await LoadCustomerAsync();
                return Page();
            }

            // 4. Find the customer record and related user.
            var customer = await _context.Customers
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.CustomerID == CustomerID);

            if (customer == null || customer.User == null)
            {
                return NotFound();
            }

            // 5. Prevent another user from using the same email.
            bool emailExists = await _context.Users.AnyAsync(u =>
                u.Email == normalizedEmail &&
                u.UserID != customer.UserID);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "This email address is already in use.");

                await LoadCustomerAsync();
                return Page();
            }

            // 6. Update the related user.
            customer.User.FullName = FullName.Trim();
            customer.User.Email = normalizedEmail;
            customer.User.Phone = Phone.Trim();

            customer.DateOfBirth = DateOfBirth;
            customer.Address = Address?.Trim() ?? string.Empty;

            // 8. Save changes.
            await _context.SaveChangesAsync();

            // 9. Return to the customer list.
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteUserAsync()
        {
            var customer = await _context.Customers
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.CustomerID == CustomerID);

            if (customer == null || customer.User == null)
            {
                return NotFound();
            }

            var user = customer.User;

            _context.Customers.Remove(customer);

            _context.Users.Remove(user);

            // 8. Save changes.
            await _context.SaveChangesAsync();

            // 9. Return to the customer list.
            return RedirectToPage();
        }
    }
}

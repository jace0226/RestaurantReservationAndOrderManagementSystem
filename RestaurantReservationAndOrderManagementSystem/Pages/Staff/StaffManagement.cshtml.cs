using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using static RestaurantReservationAndOrderManagementSystem.Pages.Admin_staff_listModel;
using System.Net.Mail;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Staff
{
    public class StaffManagementModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        private const string TemporaryPassword = "Bites123";

        public StaffManagementModel(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public class StaffViewModel
        {
            public string StaffID { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public Roles Role { get; set; }
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }

        public List<StaffViewModel> Staffs { get; set; } = new();

        // Fields submitted by the Add/Edit forms.
        [BindProperty]
        public string StaffID { get; set; } = string.Empty;

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Phone { get; set; } = string.Empty;

        [BindProperty]
        public string Role { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            await LoadStaffsAsync();
        }

        // Generate IDs such as S001, S002, U001, U002.
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

        // Load staff and related user information.
        private async Task LoadStaffsAsync()
        {
            var query =
                from staff in _context.Staffs
                join user in _context.Users
                    on staff.UserID equals user.UserID
                select new StaffViewModel
                {
                    StaffID = staff.StaffID,
                    FullName = user.FullName,
                    Email = user.Email,
                    Phone = user.Phone,
                    Role = staff.Role
                };

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                string term = SearchTerm.Trim();

                query = query.Where(s =>
                    s.StaffID.Contains(term) ||
                    s.FullName.Contains(term) ||
                    s.Email.Contains(term) ||
                    s.Phone.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(Filter) &&
                Enum.TryParse<Roles>(Filter, true, out var parsedRole) &&
                Enum.IsDefined(typeof(Roles), parsedRole))
            {
                query = query.Where(s => s.Role == parsedRole);
            }

            Staffs = await query
                .OrderBy(s => s.StaffID)
                .ToListAsync();
        }

        // POST: Add a new staff member.
        public async Task<IActionResult> OnPostAddUserAsync()
        {
            // 1. Validate required fields.
            if (string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Phone) ||
                string.IsNullOrWhiteSpace(Role))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Please fill in all fields.");

                await LoadStaffsAsync();
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

                    await LoadStaffsAsync();
                    return Page();
                }
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Please enter a valid email address.");

                await LoadStaffsAsync();
                return Page();
            }

            // 3. Validate role.
            if (!Enum.TryParse<Roles>(
                    Role.Trim(),
                    true,
                    out var parsedRole) ||
                !Enum.IsDefined(typeof(Roles), parsedRole))
            {
                ModelState.AddModelError(
                    nameof(Role),
                    "Please select a valid role.");

                await LoadStaffsAsync();
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

                await LoadStaffsAsync();
                return Page();
            }

            // 5. Generate the next StaffID and UserID.
            string newStaffID = await GenerateNextIdAsync(
                _context.Staffs.Select(s => s.StaffID),
                "S");

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

            // 7. Create the staff record linked to the user.
            var staff = new RestaurantReservationAndOrderManagementSystem.Models.Staff
            {
                StaffID = newStaffID,
                UserID = newUserID,
                Role = parsedRole,
                User = user
            };

            _context.Users.Add(user);
            _context.Staffs.Add(staff);

            // 8. Save both records.
            await _context.SaveChangesAsync();

            // 9. Return to the staff list.
            return RedirectToPage();
        }

        // POST: Edit an existing staff member.
        public async Task<IActionResult> OnPostEditUserAsync()
        {
            // 1. Validate required fields.
            if (string.IsNullOrWhiteSpace(StaffID) ||
                string.IsNullOrWhiteSpace(FullName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Phone) ||
                string.IsNullOrWhiteSpace(Role))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Please fill in all fields.");

                await LoadStaffsAsync();
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

                    await LoadStaffsAsync();
                    return Page();
                }
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Please enter a valid email address.");

                await LoadStaffsAsync();
                return Page();
            }

            // 3. Validate role.
            if (!Enum.TryParse<Roles>(
                    Role.Trim(),
                    true,
                    out var parsedRole) ||
                !Enum.IsDefined(typeof(Roles), parsedRole))
            {
                ModelState.AddModelError(
                    nameof(Role),
                    "Please select a valid role.");

                await LoadStaffsAsync();
                return Page();
            }

            // 4. Find the staff record and related user.
            var staff = await _context.Staffs
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StaffID == StaffID);

            if (staff == null || staff.User == null)
            {
                return NotFound();
            }

            // 5. Prevent another user from using the same email.
            bool emailExists = await _context.Users.AnyAsync(u =>
                u.Email == normalizedEmail &&
                u.UserID != staff.UserID);

            if (emailExists)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "This email address is already in use.");

                await LoadStaffsAsync();
                return Page();
            }

            // 6. Update the related user.
            staff.User.FullName = FullName.Trim();
            staff.User.Email = normalizedEmail;
            staff.User.Phone = Phone.Trim();

            // 7. Update the staff role.
            staff.Role = parsedRole;

            // 8. Save changes.
            await _context.SaveChangesAsync();

            // 9. Return to the staff list.
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteUserAsync() {
            var staff = await _context.Staffs
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.StaffID == StaffID);

            if (staff == null || staff.User == null)
            {
                return NotFound();
            }

            var user = staff.User;

            _context.Staffs.Remove(staff);

            _context.Users.Remove(user);

            // 8. Save changes.
            await _context.SaveChangesAsync();

            // 9. Return to the staff list.
            return RedirectToPage();
        }
    }
}
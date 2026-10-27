using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using CustomerEntity = RestaurantReservationAndOrderManagementSystem.Models.Customer;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer
{
    [Authorize(Roles = "Customer")]
    public class ProfileModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<ProfileModel> _logger;

        public ProfileModel(
            ApplicationDbContext dbContext,
            IPasswordHasher<User> passwordHasher,
            ILogger<ProfileModel> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        [BindProperty]
        public ProfileFormInput ProfileInput { get; set; } = new();

        [BindProperty]
        public ChangePasswordForm PasswordInput { get; set; } = new();

        [TempData]
        public string? ProfileSuccess { get; set; }

        [TempData]
        public string? PasswordSuccess { get; set; }

        public string LatestDateOfBirth { get; } =
            DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        public async Task<IActionResult> OnGetAsync()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                return NotFound();
            }

            PopulateProfileInput(customer);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveProfileAsync()
        {
            // 1. Fetch current customer details first
            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                return NotFound();
            }

            // 2. Re-assign disabled fields from DB so validation passes and values aren't lost
            ProfileInput.FullName = customer.User.FullName;
            ProfileInput.DateOfBirth = customer.DateOfBirth;

            // 3. Clear and validate model
            ModelState.Clear();
            NormalizeProfileInput();
            TryValidateModel(ProfileInput, nameof(ProfileInput));

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = customer.User;

            // Check unique constraints for Email & Phone
            if (await _dbContext.Users.AnyAsync(candidate =>
                    candidate.UserID != user.UserID &&
                    candidate.Email == ProfileInput.Email))
            {
                ModelState.AddModelError("ProfileInput.Email", "That email address is already in use.");
                return Page();
            }

            if (await _dbContext.Users.AnyAsync(candidate =>
                    candidate.UserID != user.UserID &&
                    candidate.Phone == ProfileInput.Phone))
            {
                ModelState.AddModelError("ProfileInput.Phone", "That phone number is already in use.");
                return Page();
            }

            // 4. Update ONLY allowed fields
            user.Email = ProfileInput.Email;
            user.Phone = ProfileInput.Phone;
            customer.Address = ProfileInput.Address;

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not update the profile for the authenticated customer.");
                ModelState.AddModelError(
                    string.Empty,
                    "We could not save your profile. Check that your email and phone number are not already registered, then try again.");
                return Page();
            }

            ProfileSuccess = "Your profile has been updated.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            ModelState.Clear();
            TryValidateModel(PasswordInput, nameof(PasswordInput));

            var customer = await GetCurrentCustomerAsync();
            if (customer is null)
            {
                return NotFound();
            }

            PopulateProfileInput(customer);

            if (!ModelState.IsValid)
            {
                ClearPasswordInput();
                return Page();
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                customer.User,
                customer.User.PasswordHash,
                PasswordInput.CurrentPassword);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(
                    "PasswordInput.CurrentPassword",
                    "The current password is incorrect.");
                ClearPasswordInput();
                return Page();
            }

            customer.User.PasswordHash = _passwordHasher.HashPassword(
                customer.User,
                PasswordInput.NewPassword);

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not change the password for the authenticated customer.");
                ModelState.AddModelError(
                    string.Empty,
                    "We could not change your password. Please try again.");
                ClearPasswordInput();
                return Page();
            }

            PasswordSuccess = "Your password has been changed.";
            return RedirectToPage();
        }

        private async Task<CustomerEntity?> GetCurrentCustomerAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            return await _dbContext.Customers
                .Include(customer => customer.User)
                .SingleOrDefaultAsync(customer => customer.UserID == userId);
        }

        private void PopulateProfileInput(CustomerEntity customer)
        {
            ProfileInput = new ProfileFormInput
            {
                FullName = customer.User.FullName,
                Email = customer.User.Email,
                Phone = customer.User.Phone,
                DateOfBirth = customer.DateOfBirth,
                Address = customer.Address
            };
        }

        private void NormalizeProfileInput()
        {
            ProfileInput.FullName = ProfileInput.FullName?.Trim() ?? string.Empty;
            ProfileInput.Email = ProfileInput.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            ProfileInput.Phone = ProfileInput.Phone?.Trim() ?? string.Empty;
            ProfileInput.Address = ProfileInput.Address?.Trim() ?? string.Empty;
        }

        private void ClearPasswordInput()
        {
            PasswordInput.CurrentPassword = string.Empty;
            PasswordInput.NewPassword = string.Empty;
            PasswordInput.ConfirmPassword = string.Empty;
        }

        public sealed class ProfileFormInput
        {
            [Required(ErrorMessage = "Full name is required.")]
            [StringLength(120)]
            [Display(Name = "Full name")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [StringLength(254)]
            [Display(Name = "Email address")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone number is required.")]
            [Phone(ErrorMessage = "Please enter a valid phone number.")]
            [StringLength(32)]
            [Display(Name = "Phone number")]
            public string Phone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Date of birth is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Date of birth")]
            public DateOnly? DateOfBirth { get; set; }

            [StringLength(500)]
            public string Address { get; set; } = string.Empty;
        }

        public sealed class ChangePasswordForm
        {
            [Required(ErrorMessage = "Current password is required.")]
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "New password is required.")]
            [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your new password.")]
            [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using CustomerModel = RestaurantReservationAndOrderManagementSystem.Models.Customer;

namespace RestaurantReservationAndOrderManagementSystem.Pages
{
    public class authenticationModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<authenticationModel> _logger;

        public authenticationModel(
            ApplicationDbContext dbContext,
            IPasswordHasher<User> passwordHasher,
            ILogger<authenticationModel> logger)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        [BindProperty]
        public LoginInput Login { get; set; } = new();

        [BindProperty]
        public RegistrationInput Registration { get; set; } = new();

        public string ActiveForm { get; private set; } = "login";

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostLoginAsync(string? returnUrl)
        {
            ActiveForm = "login";
            ModelState.Clear();
            if (!TryValidateModel(Login, nameof(Login)))
            {
                return Page();
            }

            var identifier = Login.EmailOrPhone.Trim();
            var isEmail = identifier.Contains('@');
            if (isEmail && !new EmailAddressAttribute().IsValid(identifier))
            {
                ModelState.AddModelError("Login.EmailOrPhone", "Enter a valid email address or phone number.");
                return Page();
            }

            var normalizedIdentifier = isEmail ? identifier.ToLowerInvariant() : identifier;
            var user = await _dbContext.Users.SingleOrDefaultAsync(candidate =>
                isEmail
                    ? candidate.Email == normalizedIdentifier
                    : candidate.Phone == normalizedIdentifier);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "The email/phone number or password is incorrect.");
                return Page();
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                Login.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "The email/phone number or password is incorrect.");
                return Page();
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, Login.Password);
                await _dbContext.SaveChangesAsync();
            }

            var claims = await CreateClaimsAsync(user);
            if (claims is null)
            {
                ModelState.AddModelError(string.Empty, "This account is not configured for sign-in. Please contact support.");
                return Page();
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            var role = claims.Single(claim => claim.Type == ClaimTypes.Role).Value;
            if (role is "Staff" or "Administrator")
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) &&
                    Url.IsLocalUrl(returnUrl) &&
                    Uri.TryCreate(returnUrl, UriKind.Relative, out _) &&
                    returnUrl.StartsWith("/Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }

                return Redirect("/Staff/Dashboard");
            }

            return Redirect("/");
        }

        public async Task<IActionResult> OnPostRegisterAsync()
        {
            ActiveForm = "register";
            ModelState.Clear();
            if (!TryValidateModel(Registration, nameof(Registration)))
            {
                return Page();
            }

            var email = Registration.Email.Trim().ToLowerInvariant();
            var phone = Registration.Phone.Trim();
            if (Registration.DateOfBirth!.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                ModelState.AddModelError("Registration.DateOfBirth", "Date of birth cannot be in the future.");
                return Page();
            }

            if (await _dbContext.Users.AnyAsync(user => user.Email == email || user.Phone == phone))
            {
                ModelState.AddModelError(string.Empty, "An account with that email address or phone number already exists.");
                return Page();
            }

            var user = new User
            {
                UserID = Guid.NewGuid().ToString("N"),
                FullName = Registration.FullName.Trim(),
                Email = email,
                Phone = phone
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, Registration.Password);

            var customer = new CustomerModel
            {
                CustomerID = Guid.NewGuid().ToString("N"),
                UserID = user.UserID,
                DateOfBirth = Registration.DateOfBirth.Value
            };

            _dbContext.Users.Add(user);
            _dbContext.Customers.Add(customer);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not create a customer account.");
                ModelState.AddModelError(string.Empty, "We could not create your account. Check whether your email or phone number is already registered and try again.");
                return Page();
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.UserID),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, "Customer")
                ],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Redirect("/");
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/Authenticate");
        }

        private async Task<List<Claim>?> CreateClaimsAsync(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserID),
                new(ClaimTypes.Name, user.FullName),
                new(ClaimTypes.Email, user.Email)
            };

            if (await _dbContext.Customers.AnyAsync(customer => customer.UserID == user.UserID))
            {
                claims.Add(new Claim(ClaimTypes.Role, "Customer"));
                return claims;
            }

            var staffRole = await _dbContext.Staffs
                .Where(staff => staff.UserID == user.UserID)
                .Select(staff => (Roles?)staff.Role)
                .SingleOrDefaultAsync();
            if (staffRole.HasValue)
            {
                claims.Add(new Claim(
                    ClaimTypes.Role,
                    staffRole.Value == Roles.Admin ? "Administrator" : "Staff"));
                return claims;
            }

            return null;
        }

        public sealed class LoginInput
        {
            [Required(ErrorMessage = "Enter your email address or phone number.")]
            [Display(Name = "Email or phone number")]
            public string EmailOrPhone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;
        }

        public sealed class RegistrationInput
        {
            [Required(ErrorMessage = "Full name is required.")]
            [StringLength(120)]
            [Display(Name = "Full name")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [StringLength(254)]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your password.")]
            [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone number is required.")]
            [Phone(ErrorMessage = "Please enter a valid phone number.")]
            [StringLength(32)]
            [Display(Name = "Phone number")]
            public string Phone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Date of birth is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Date of birth")]
            public DateOnly? DateOfBirth { get; set; }
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
        private const string CaptchaSessionKey = "CaptchaCode";
        private const string CaptchaAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const int CaptchaLength = 6;

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
        public string CaptchaImage { get; private set; } = string.Empty;

        public void OnGet()
        {
            ResetCaptcha();
        }

        public IActionResult OnGetGenerateCaptcha()
        {
            var code = GenerateCaptchaCode();
            HttpContext.Session.SetString(CaptchaSessionKey, code);
            Response.Headers.CacheControl = "no-store, no-cache";
            return Content(CreateCaptchaSvg(code), "image/svg+xml");
        }

        public async Task<IActionResult> OnPostLoginAsync(string? returnUrl)
        {
            ActiveForm = "login";
            ModelState.Clear();

            var storedCaptcha = HttpContext.Session.GetString(CaptchaSessionKey);
            var userEnteredCaptcha = Login.CaptchaCode;
            if (string.IsNullOrEmpty(userEnteredCaptcha) ||
                string.IsNullOrEmpty(storedCaptcha) ||
                !string.Equals(userEnteredCaptcha, storedCaptcha, StringComparison.Ordinal))
            {
                ModelState.AddModelError(
                    "Login.CaptchaCode",
                    "Invalid CAPTCHA code. Please enter the exact characters shown.");
                ResetCaptcha();
                return Page();
            }

            CaptchaImage = CreateCaptchaImage(storedCaptcha);
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
                isEmail ? candidate.Email == normalizedIdentifier : candidate.Phone == normalizedIdentifier);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "The email/phone number or password is incorrect.");
                return Page();
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Login.Password);
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
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            HttpContext.Session.Remove(CaptchaSessionKey);

            var role = claims.Single(claim => claim.Type == ClaimTypes.Role).Value;
            if (role is "Staff" or "Administrator")
            {
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Relative, out _) && returnUrl.StartsWith("/Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl!);
                }

                return Redirect("/Staff/Dashboard");
            }

            if (role == "Customer")
            {
                if (IsReturnUrlForArea(returnUrl, "Customer"))
                {
                    return Redirect(returnUrl!);
                }

                return Redirect("/Customer/Menus");
            }

            return Redirect("/Authenticate");
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
            if (Registration.DateOfBirth!.Value > DateOnly.FromDateTime(DateTime.Today))
            {
                ModelState.AddModelError("Registration.DateOfBirth", "Date of Birth cannot be a future date.");
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

            TempData["RegisterSuccess"] = true;
            return Redirect("/Authenticate");
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

        private bool IsReturnUrlForArea(string? returnUrl, string area)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl) &&
                Uri.TryCreate(returnUrl, UriKind.Relative, out _) &&
                (returnUrl.Equals($"/{area}", StringComparison.OrdinalIgnoreCase) ||
                 returnUrl.StartsWith($"/{area}/", StringComparison.OrdinalIgnoreCase) ||
                 returnUrl.StartsWith($"/{area}?", StringComparison.OrdinalIgnoreCase));
        }

        private string GenerateCaptchaCode()
        {
            var code = new StringBuilder(CaptchaLength);
            for (var index = 0; index < CaptchaLength; index++)
            {
                code.Append(CaptchaAlphabet[RandomNumberGenerator.GetInt32(CaptchaAlphabet.Length)]);
            }

            return code.ToString();
        }

        private void ResetCaptcha()
        {
            var code = GenerateCaptchaCode();
            HttpContext.Session.SetString(CaptchaSessionKey, code);
            CaptchaImage = CreateCaptchaImage(code);
        }

        private static string CreateCaptchaImage(string code)
        {
            return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(CreateCaptchaSvg(code)))}";
        }

        private static string CreateCaptchaSvg(string code)
        {
            var random = new Random(HashCode.Combine(code, DateTime.UtcNow.Ticks));
            var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"250\" height=\"76\" viewBox=\"0 0 250 76\">");
            svg.Append("<rect width=\"250\" height=\"76\" rx=\"10\" fill=\"#eef4f8\"/>");

            for (var index = 0; index < 7; index++)
            {
                var x1 = random.Next(0, 250);
                var y1 = random.Next(8, 68);
                var x2 = random.Next(0, 250);
                var y2 = random.Next(8, 68);
                svg.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#8eb4ca\" stroke-width=\"{random.Next(1, 3)}\" opacity=\".55\"/>");
            }

            for (var index = 0; index < 35; index++)
            {
                svg.Append($"<circle cx=\"{random.Next(5, 245)}\" cy=\"{random.Next(5, 71)}\" r=\"{random.Next(1, 3)}\" fill=\"#47758e\" opacity=\".45\"/>");
            }

            for (var index = 0; index < code.Length; index++)
            {
                var x = 20 + index * 38;
                var y = random.Next(48, 58);
                var rotation = random.Next(-16, 17);
                var color = index % 2 == 0 ? "#0a315b" : "#ff5634";
                svg.Append($"<text x=\"{x}\" y=\"{y}\" transform=\"rotate({rotation} {x} {y})\" fill=\"{color}\" font-family=\"Arial, sans-serif\" font-size=\"34\" font-weight=\"800\">{code[index]}</text>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        }

        public sealed class LoginInput
        {
            [Required(ErrorMessage = "Enter your email address or phone number.")]
            [Display(Name = "Email or phone number")]
            public string EmailOrPhone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Enter the CAPTCHA code.")]
            [StringLength(CaptchaLength, MinimumLength = CaptchaLength, ErrorMessage = "Enter the 6-character CAPTCHA code.")]
            [RegularExpression("^[A-Za-z0-9]{6}$", ErrorMessage = "Enter a 6-character alphanumeric CAPTCHA code.")]
            [Display(Name = "CAPTCHA code")]
            public string CaptchaCode { get; set; } = string.Empty;
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

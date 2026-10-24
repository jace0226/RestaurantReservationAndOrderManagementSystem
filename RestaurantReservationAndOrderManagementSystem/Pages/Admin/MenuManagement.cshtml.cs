using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Admin
{
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public class MenuManagementModel : PageModel
    {
        private const long MaxImageSize = 5 * 1024 * 1024;
        private static readonly string[] DefaultCategories =
            ["Main Course", "Appetizers", "Desserts", "Beverages"];
        private static readonly string[] AvailabilityOptions = ["Available", "Unavailable"];

        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<MenuManagementModel> _logger;

        public MenuManagementModel(
            ApplicationDbContext dbContext,
            IWebHostEnvironment environment,
            ILogger<MenuManagementModel> logger)
        {
            _dbContext = dbContext;
            _environment = environment;
            _logger = logger;
        }

        [BindProperty]
        public MenuItemInput Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public List<MenuItemRow> MenuItems { get; private set; } = [];
        public List<string> Categories { get; private set; } = [];
        public bool ShowForm { get; private set; }
        public string? CurrentImageUrl { get; private set; }

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            ShowForm = true;
            await LoadCategoriesAsync();

            if (!AvailabilityOptions.Contains(Input.Availability, StringComparer.Ordinal))
            {
                ModelState.AddModelError("Input.Availability", "Select a valid availability.");
            }

            if (!Categories.Contains(Input.Category, StringComparer.Ordinal))
            {
                ModelState.AddModelError("Input.Category", "Select a valid menu category.");
            }

            Food_Items? existingItem = null;
            if (!string.IsNullOrWhiteSpace(Input.FoodId))
            {
                existingItem = await _dbContext.FoodItems
                    .SingleOrDefaultAsync(item => item.FoodID == Input.FoodId);
                if (existingItem is null)
                {
                    return NotFound();
                }

                CurrentImageUrl = existingItem.ImageUrl;
            }
            else if (Input.Image is null)
            {
                ModelState.AddModelError("Input.Image", "Select an image for the menu item.");
            }

            string? uploadExtension = null;
            if (Input.Image is not null)
            {
                var imageReadFailed = false;
                try
                {
                    uploadExtension = await ValidateImageAsync(Input.Image);
                }
                catch (IOException exception)
                {
                    _logger.LogError(exception, "Could not inspect uploaded menu image.");
                    ModelState.AddModelError("Input.Image", "The image could not be read. Please try another image.");
                    imageReadFailed = true;
                }
                catch (UnauthorizedAccessException exception)
                {
                    _logger.LogError(exception, "Could not access uploaded menu image.");
                    ModelState.AddModelError("Input.Image", "The image could not be read. Please try another image.");
                    imageReadFailed = true;
                }

                if (uploadExtension is null && !imageReadFailed)
                {
                    ModelState.AddModelError(
                        "Input.Image",
                        "Choose a JPG, PNG, or WebP image smaller than 5 MB.");
                }
            }

            if (!TryValidateModel(Input, nameof(Input)) || !ModelState.IsValid)
            {
                await LoadPageDataAsync();
                return Page();
            }

            string? staffId = null;
            if (existingItem is null)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                staffId = await _dbContext.Staffs
                    .Where(staff => staff.UserID == userId)
                    .Select(staff => staff.StaffID)
                    .SingleOrDefaultAsync();

                if (staffId is null)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Your account is not linked to a staff record, so menu items cannot be added.");
                    await LoadPageDataAsync();
                    return Page();
                }
            }

            string? newImageUrl = null;
            var oldImageUrl = existingItem?.ImageUrl;
            try
            {
                if (Input.Image is not null && uploadExtension is not null)
                {
                    newImageUrl = await SaveImageAsync(Input.Image, uploadExtension);
                }

                var menuItem = existingItem ?? new Food_Items
                {
                    FoodID = Guid.NewGuid().ToString("N"),
                    StaffID = staffId!
                };

                menuItem.FoodName = Input.Name.Trim();
                menuItem.Price = Input.Price;
                menuItem.FoodDescription = Input.Description?.Trim() ?? string.Empty;
                menuItem.FullDescription = Input.FullDescription?.Trim() ?? string.Empty;
                menuItem.Category = Input.Category;
                menuItem.Dietary = Input.Dietary.Trim();
                menuItem.PrepTime = $"{Input.PrepTime.Trim()} mins";
                menuItem.FoodAvailability = Input.Availability;
                menuItem.ImageAlt = Input.Name.Trim();

                if (newImageUrl is not null)
                {
                    menuItem.ImageUrl = newImageUrl;
                }

                if (existingItem is null)
                {
                    _dbContext.FoodItems.Add(menuItem);
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not save menu item {FoodName}.", Input.Name);
                DeleteImageFile(newImageUrl);
                ModelState.AddModelError(string.Empty, "The menu item could not be saved. Please try again.");
                await LoadPageDataAsync();
                return Page();
            }
            catch (IOException exception)
            {
                _logger.LogError(exception, "Could not store the image for menu item {FoodName}.", Input.Name);
                ModelState.AddModelError(string.Empty, "The image could not be saved. Please try another image.");
                await LoadPageDataAsync();
                return Page();
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogError(exception, "The application could not access the menu image folder.");
                ModelState.AddModelError(string.Empty, "The image could not be saved. Please try again later.");
                await LoadPageDataAsync();
                return Page();
            }

            if (existingItem is not null && newImageUrl is not null)
            {
                await DeleteImageIfUnusedAsync(oldImageUrl, existingItem.FoodID);
            }

            TempData["SuccessMessage"] = existingItem is null
                ? "Menu item added successfully."
                : "Menu item updated successfully.";
            return RedirectToPage("/Admin/MenuManagement", new { search = Search });
        }

        public async Task<IActionResult> OnPostDeleteAsync(string? foodId)
        {
            if (string.IsNullOrWhiteSpace(foodId))
            {
                return BadRequest();
            }

            var menuItem = await _dbContext.FoodItems
                .SingleOrDefaultAsync(item => item.FoodID == foodId);
            if (menuItem is null)
            {
                return NotFound();
            }

            var imageUrl = menuItem.ImageUrl;
            _dbContext.FoodItems.Remove(menuItem);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                _logger.LogError(exception, "Could not delete menu item {FoodId}.", foodId);
                TempData["ErrorMessage"] = "The menu item could not be deleted. It may still be in use.";
                return RedirectToPage("/Admin/MenuManagement", new { search = Search });
            }

            var imageRemoved = await DeleteImageIfUnusedAsync(imageUrl, foodId);
            TempData["SuccessMessage"] = imageRemoved
                ? "Menu item deleted successfully."
                : "Menu item deleted, but its image could not be removed.";
            return RedirectToPage("/Admin/MenuManagement", new { search = Search });
        }

        private async Task LoadPageDataAsync()
        {
            var items = await _dbContext.FoodItems
                .AsNoTracking()
                .OrderBy(item => item.FoodName)
                .ToListAsync();

            MenuItems = items.Select(item => new MenuItemRow(
                item.FoodID,
                item.FoodName,
                item.Price,
                item.FoodDescription,
                item.FullDescription,
                item.Category,
                item.Dietary,
                item.PrepTime,
                item.FoodAvailability,
                item.ImageUrl,
                item.ImageAlt)).ToList();

            await LoadCategoriesAsync(items);

            if (ShowForm && !string.IsNullOrWhiteSpace(Input.FoodId))
            {
                CurrentImageUrl = items
                    .SingleOrDefault(item => item.FoodID == Input.FoodId)
                    ?.ImageUrl;
            }
        }

        private async Task LoadCategoriesAsync(List<Food_Items>? items = null)
        {
            items ??= await _dbContext.FoodItems
                .AsNoTracking()
                .ToListAsync();

            Categories = DefaultCategories
                .Concat(items.Select(item => item.Category))
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private async Task<string?> ValidateImageAsync(IFormFile image)
        {
            if (image.Length is <= 0 or > MaxImageSize)
            {
                return null;
            }

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            {
                return null;
            }

            var header = new byte[12];
            await using var stream = image.OpenReadStream();
            var bytesRead = await stream.ReadAsync(header.AsMemory());
            var isJpeg = bytesRead >= 3 &&
                         header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            var isPng = bytesRead >= 8 &&
                        header[0] == 137 && header[1] == 80 && header[2] == 78 && header[3] == 71 &&
                        header[4] == 13 && header[5] == 10 && header[6] == 26 && header[7] == 10;
            var isWebp = bytesRead >= 12 &&
                         header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                         header.AsSpan(8, 4).SequenceEqual("WEBP"u8);

            if ((extension is ".jpg" or ".jpeg") && isJpeg)
            {
                return ".jpg";
            }

            if (extension == ".png" && isPng)
            {
                return ".png";
            }

            return extension == ".webp" && isWebp ? ".webp" : null;
        }

       private async Task<string> SaveImageAsync(IFormFile image, string extension)
        {
            var imageDirectory = GetImageDirectory();
            Directory.CreateDirectory(imageDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(imageDirectory, fileName);
                var imageUrl = $"/images/{fileName}";
                try
                {
                await using var destination = System.IO.File.Create(filePath);
                    await image.CopyToAsync(destination);
                    return imageUrl;
                }
                catch (IOException)
                {
                    DeleteImageFile(imageUrl);
                    throw;
                }
                catch (UnauthorizedAccessException)
                {
                    DeleteImageFile(imageUrl);
                    throw;
                }
        }

        private string GetImageDirectory()
        {
            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            return Path.Combine(webRoot, "images");
        }

        private async Task<bool> DeleteImageIfUnusedAsync(string? imageUrl, string foodId)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                await _dbContext.FoodItems.AnyAsync(item =>
                    item.FoodID != foodId && item.ImageUrl == imageUrl))
            {
                return true;
            }

            return DeleteImageFile(imageUrl);
        }

        private bool DeleteImageFile(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith("/images/", StringComparison.Ordinal))
            {
                return true;
            }

            var fileName = imageUrl["/images/".Length..];
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            {
                return true;
            }

            var path = Path.Combine(GetImageDirectory(), fileName);
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }

                return true;
            }
            catch (IOException exception)
            {
                _logger.LogWarning(exception, "Could not remove menu image {ImageFile}.", fileName);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                _logger.LogWarning(exception, "Could not access menu image {ImageFile} for removal.", fileName);
                return false;
            }
        }

        public sealed class MenuItemInput
        {
            public string? FoodId { get; set; }

            [Required(ErrorMessage = "Enter a food name.")]
            [StringLength(120)]
            public string Name { get; set; } = string.Empty;

            [Range(typeof(decimal), "0.01", "999999.99", ErrorMessage = "Enter a positive price.")]
            public decimal Price { get; set; }

            [StringLength(300, ErrorMessage = "The short description must be 300 characters or fewer.")]
            public string? Description { get; set; }

            [StringLength(2000, ErrorMessage = "The full description must be 2,000 characters or fewer.")]
            public string? FullDescription { get; set; }

            [Required(ErrorMessage = "Select a category.")]
            [StringLength(80)]
            public string Category { get; set; } = DefaultCategories[0];

            [Required(ErrorMessage = "Enter dietary information.")]
            [StringLength(100)]
            public string Dietary { get; set; } = "Standard";

            [Required(ErrorMessage = "Enter a preparation time in minutes.")]
            [RegularExpression(
                @"^[1-9]\d{0,2}(?:\s*-\s*[1-9]\d{0,2})?$",
                ErrorMessage = "Enter preparation time in minutes, such as 20 or 15-20.")]
            public string PrepTime { get; set; } = "15";

            [Required(ErrorMessage = "Select availability.")]
            public string Availability { get; set; } = "Available";

            public IFormFile? Image { get; set; }
        }

        public sealed record MenuItemRow(
            string Id,
            string Name,
            decimal Price,
            string Description,
            string FullDescription,
            string Category,
            string Dietary,
            string PrepTime,
            string Availability,
            string Image,
            string ImageAlt);
    }
}

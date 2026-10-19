using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Models;

namespace RestaurantReservationAndOrderManagementSystem.Data;

public static class AccountSeeder
{
    public static async Task SeedConfiguredAccountsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(AccountSeeder));

        await SeedAccountAsync(
            configuration.GetSection("SeedAccounts:Staff"),
            Roles.Staff,
            dbContext,
            passwordHasher,
            logger);
        await SeedAccountAsync(
            configuration.GetSection("SeedAccounts:Administrator"),
            Roles.Admin,
            dbContext,
            passwordHasher,
            logger);
    }

    private static async Task SeedAccountAsync(
        IConfigurationSection accountConfiguration,
        Roles role,
        ApplicationDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        ILogger logger)
    {
        var roleName = role == Roles.Admin ? "Administrator" : "Staff";
        var fullName = accountConfiguration["FullName"];
        var email = accountConfiguration["Email"]?.Trim().ToLowerInvariant();
        var phone = accountConfiguration["Phone"]?.Trim();
        var password = accountConfiguration["Password"];

        if (string.IsNullOrWhiteSpace(fullName) &&
            string.IsNullOrWhiteSpace(email) &&
            string.IsNullOrWhiteSpace(phone) &&
            string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No {Role} seed account configured. Set SeedAccounts:{Role}:FullName, Email, Phone, and Password to provision it.",
                roleName,
                roleName);
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"The {roleName} seed account requires FullName, Email, Phone, and Password configuration.");
        }

        if (!new EmailAddressAttribute().IsValid(email) ||
            email.Length > 254 ||
            !new PhoneAttribute().IsValid(phone) ||
            phone.Length > 32 ||
            password.Length < 8)
        {
            throw new InvalidOperationException(
                $"The {roleName} seed account must use a valid email and phone number, and a password of at least 8 characters.");
        }

        var matchingUsers = await dbContext.Users
            .Where(user => user.Email == email || user.Phone == phone)
            .Take(2)
            .ToListAsync();
        if (matchingUsers.Count > 1)
        {
            logger.LogWarning(
                "Skipping {Role} seed account because its configured email and phone number belong to different accounts.",
                roleName);
            return;
        }

        var existingUser = matchingUsers.SingleOrDefault();
        if (existingUser is not null)
        {
            var isExpectedStaffAccount =
                existingUser.Email == email &&
                existingUser.Phone == phone &&
                !await dbContext.Customers.AnyAsync(customer => customer.UserID == existingUser.UserID) &&
                await dbContext.Staffs.AnyAsync(staff =>
                    staff.UserID == existingUser.UserID && staff.Role == role);

            if (!isExpectedStaffAccount)
            {
                logger.LogWarning(
                    "Skipping {Role} seed account because its configured email or phone is already associated with a different account.",
                    roleName);
            }

            return;
        }

        var user = new User
        {
            UserID = Guid.NewGuid().ToString("N"),
            FullName = fullName.Trim(),
            Email = email,
            Phone = phone
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        dbContext.Staffs.Add(new Staff
        {
            StaffID = Guid.NewGuid().ToString("N"),
            UserID = user.UserID,
            Role = role
        });

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded the configured {Role} account.", roleName);
    }
}

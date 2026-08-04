using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Authentication;

namespace Nagomi.Api.Infrastructure.Identity;

public static class UserSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var role in NagomiRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole(role));
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.Users.AnyAsync(x => x.IsActive))
            return;

        var options = configuration
            .GetSection(UserAuthenticationOptions.SectionName)
            .Get<UserAuthenticationOptions>() ?? new();
        var email = string.IsNullOrWhiteSpace(options.AdminEmail) ? "admin@nagomi.local" : options.AdminEmail.Trim();
        var password = string.IsNullOrWhiteSpace(options.AdminPassword) ? "change-me-admin-123" : options.AdminPassword;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Administrador",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        if ((await userManager.CreateAsync(admin, password)).Succeeded)
            await userManager.AddToRoleAsync(admin, NagomiRoles.Admin);
    }
}

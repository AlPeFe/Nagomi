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
        if (await userManager.Users.IgnoreQueryFilters().AnyAsync())
            return;

        var options = configuration
            .GetSection(UserAuthenticationOptions.SectionName)
            .Get<UserAuthenticationOptions>() ?? new();
        var userName = string.IsNullOrWhiteSpace(options.BootstrapUserName) ? "admin" : options.BootstrapUserName.Trim();
        var password = string.IsNullOrWhiteSpace(options.BootstrapPassword) ? "Admin" : options.BootstrapPassword;
        var email = string.IsNullOrWhiteSpace(options.AdminEmail) ? "admin@nagomi.local" : options.AdminEmail.Trim();

        // First-run bootstrap admin (admin / Admin by default). It MUST change credentials via the
        // onboarding flow: the real admin account is created there and this one is deactivated.
        // The password hash is set directly so the bootstrap can bypass the 12-char policy; the
        // policy applies to every real account created afterwards.
        var admin = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            DisplayName = "Administrador inicial",
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow,
            PasswordHash = userManager.PasswordHasher.HashPassword(new ApplicationUser(), password)
        };
        if ((await userManager.CreateAsync(admin)).Succeeded)
            await userManager.AddToRoleAsync(admin, NagomiRoles.Admin);
    }
}

using Microsoft.AspNetCore.Identity;

namespace Nagomi.Api.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// True for the bootstrap admin (admin / Admin) created on a fresh install. That account can
    /// ONLY complete the onboarding (create the real admin); every other endpoint returns 403 and
    /// the account is deactivated once onboarding finishes.
    /// </summary>
    public bool MustChangePassword { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}

public static class NagomiRoles
{
    public const string Admin = "admin";
    public const string Default = "default";

    public static readonly IReadOnlyList<string> All = [Admin, Default];
}

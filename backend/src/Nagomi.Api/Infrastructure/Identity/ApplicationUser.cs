using Microsoft.AspNetCore.Identity;

namespace Nagomi.Api.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
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

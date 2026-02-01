using Microsoft.AspNetCore.Identity;

namespace HM.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user. Used only for authentication.
/// Domain User (HM.Domain.Entities.User) holds business-level info and shares the same Id (Guid).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
}

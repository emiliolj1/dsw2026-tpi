using Microsoft.AspNetCore.Identity;

namespace Dsw2026Tpi.Data.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public bool Deleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

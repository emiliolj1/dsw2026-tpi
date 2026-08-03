using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Data.Identity;

public class AuthenticationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AuthenticationDbContext(
        DbContextOptions<AuthenticationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UsersRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UsersClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UsersLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RolesClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UsersTokens");
    }
}

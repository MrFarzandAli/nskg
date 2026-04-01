using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nskg.Models.Security;

namespace Nskg.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        DbSet<Form> Forms { get; set; }
        DbSet<RoleFormPermission> RoleFormPermissions { get; set; }

    }

}

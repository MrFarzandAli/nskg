using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nskg.Models;
using Nskg.Models.Security;

namespace Nskg.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Form> Forms { get; set; }
        public DbSet<RoleFormPermission> RoleFormPermissions { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<FinancialYear> FinancialYears { get; set; }
        public DbSet<UserCompany> UserCompanies { get; set; }
        public DbSet<Actype> Actype { get; set; }
        public DbSet<AccCat> AccCat { get; set; }
        public DbSet<GLChart1> GLChart1 { get; set; }
        public DbSet<GLChart3> GLChart3 { get; set; }
        public DbSet<AcPara> AcPara { get; set; }


        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // 🔥 FIX
            builder.Ignore<IdentityPasskeyData>();

            // Your existing relation
            builder.Entity<GLChart3>()
                .HasOne(x => x.GLChart1)
                .WithMany(x => x.GLChart3s)
                .HasForeignKey(x => x.GLChart1Id)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

}

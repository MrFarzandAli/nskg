using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nskg.Models;
using Nskg.Models.Security;
using System.Reflection.Emit;
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
        public DbSet<VoHead> VoHead { get; set; }
        public DbSet<VoDet> VoDet { get; set; }
        public DbSet<IssHead> IssHead { get; set; }
        public DbSet<IssDetail> IssDetail { get; set; }
        public DbSet<GLTrans> GLTrans { get; set; }
        public DbSet<VoucherTypeSetting> VoucherTypeSettings { get; set; }
        public DbSet<OpeningBalance> OpeningBalances { get; set; }

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

            builder.Entity<VoHead>()
               .HasMany(x => x.Details)
               .WithOne(x => x.VoHead)
               .HasForeignKey(x => x.VoHeadId)
               .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AcPara>()
                .HasOne(a => a.Actype)
                .WithMany(p => p.acParas)    // explicitly bind to Actype.acParas
                .HasForeignKey(a => a.ActypeCode)
                .HasPrincipalKey(p => p.ACTYPE)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AcPara>()
                .HasOne(a => a.GLChart1)
                .WithMany(g => g.acParas)           // explicitly match GLChart1.acParas
                .HasForeignKey(a => a.GLChart1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AcPara>()
                .HasOne(a => a.GLChart3)
                .WithMany(g => g.acParas)           // explicitly match GLChart3.acParas
                .HasForeignKey(a => a.GLChart3Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<IssHead>()
                .HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<IssHead>()
                .HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

}

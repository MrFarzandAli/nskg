using Microsoft.AspNetCore.Identity;

namespace Nskg.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRoles(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            string[] roles = { "Admin", "Manager", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed default Admin user
            string adminEmail = "admin@nskg.com";
            string adminPassword = "Admin@123";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                var newAdmin = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(newAdmin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }

            // Seed Sample CommBook Data if empty
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            if (!context.CommHead.Any())
            {
                var head1 = new Models.CommHead
                {
                    DocNo = "CB-2026-001",
                    DocDate = DateTime.Now.AddDays(-5),
                    ChalNo = 101,
                    Station = "Karachi Adda",
                    VehicleNo = "KBL-1234",
                    Driver = "Muhammad Ali",
                    Transporter = "Shadab Transport",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed",
                    IsDeleted = false
                };

                var head2 = new Models.CommHead
                {
                    DocNo = "CB-2026-002",
                    DocDate = DateTime.Now.AddDays(-2),
                    ChalNo = 102,
                    Station = "Lahore Godown",
                    VehicleNo = "LES-5678",
                    Driver = "Tariq Mahmood",
                    Transporter = "Niazi Goods",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed",
                    IsDeleted = false
                };

                var head3 = new Models.CommHead
                {
                    DocNo = "CB-2026-003",
                    DocDate = DateTime.Now,
                    ChalNo = 103,
                    Station = "Multan Station",
                    VehicleNo = "MNA-9012",
                    Driver = "Rashid Khan",
                    Transporter = "Royal Cargo",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed",
                    IsDeleted = false
                };

                var head4 = new Models.CommHead
                {
                    DocNo = "CB-2026-004",
                    DocDate = DateTime.Now.AddDays(-10),
                    ChalNo = 104,
                    Station = "Rawalpindi Godown",
                    VehicleNo = "RIL-3344",
                    Driver = "Zubair Ahmad",
                    Transporter = "Khyber Goods",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed",
                    IsDeleted = false
                };

                var head5 = new Models.CommHead
                {
                    DocNo = "CB-2026-005",
                    DocDate = DateTime.Now.AddDays(-1),
                    ChalNo = 105,
                    Station = "Faisalabad Adda",
                    VehicleNo = "FSD-7788",
                    Driver = "Kamran Shah",
                    Transporter = "Punjab Logistics",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed",
                    IsDeleted = false
                };

                context.CommHead.AddRange(head1, head2, head3, head4, head5);
                await context.SaveChangesAsync();

                var det1 = new Models.CommDetail
                {
                    CommHeadId = head1.Id,
                    BillTiNo = 5001,
                    BilNo = 1001,
                    DocDate = DateTime.Now.AddDays(-5),
                    VehicleNo = "KBL-1234",
                    MAmount = 45000.00m,
                    NetAmt = 42500.00m,
                    CommPer = 5.00m,
                    DeliveryAmt = 1500.00m,
                    DeliveryAmt2 = 300.00m,
                    STaxAmt = 2250.00m,
                    Qty = 150,
                    CusName = "Al-Rahman Traders",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed"
                };

                var det2 = new Models.CommDetail
                {
                    CommHeadId = head2.Id,
                    BillTiNo = 5002,
                    BilNo = 1002,
                    DocDate = DateTime.Now.AddDays(-2),
                    VehicleNo = "LES-5678",
                    MAmount = 88000.00m,
                    NetAmt = 83600.00m,
                    CommPer = 5.00m,
                    DeliveryAmt = 2500.00m,
                    DeliveryAmt2 = 500.00m,
                    STaxAmt = 4400.00m,
                    Qty = 320,
                    CusName = "Pakistan Textile Corp",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed"
                };

                var det3 = new Models.CommDetail
                {
                    CommHeadId = head3.Id,
                    BillTiNo = 5003,
                    BilNo = 1003,
                    DocDate = DateTime.Now,
                    VehicleNo = "MNA-9012",
                    MAmount = 62000.00m,
                    NetAmt = 58900.00m,
                    CommPer = 5.00m,
                    DeliveryAmt = 1800.00m,
                    DeliveryAmt2 = 360.00m,
                    STaxAmt = 3100.00m,
                    Qty = 210,
                    CusName = "Bismillah Auto Parts",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed"
                };

                var det4 = new Models.CommDetail
                {
                    CommHeadId = head4.Id,
                    BillTiNo = 5004,
                    BilNo = 1004,
                    DocDate = DateTime.Now.AddDays(-10),
                    VehicleNo = "RIL-3344",
                    MAmount = 95000.00m,
                    NetAmt = 90250.00m,
                    CommPer = 5.00m,
                    DeliveryAmt = 3000.00m,
                    DeliveryAmt2 = 600.00m,
                    STaxAmt = 4750.00m,
                    Qty = 400,
                    CusName = "Northern Hardware Mart",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed"
                };

                var det5 = new Models.CommDetail
                {
                    CommHeadId = head5.Id,
                    BillTiNo = 5005,
                    BilNo = 1005,
                    DocDate = DateTime.Now.AddDays(-1),
                    VehicleNo = "FSD-7788",
                    MAmount = 73000.00m,
                    NetAmt = 69350.00m,
                    CommPer = 5.00m,
                    DeliveryAmt = 2100.00m,
                    DeliveryAmt2 = 420.00m,
                    STaxAmt = 3650.00m,
                    Qty = 280,
                    CusName = "Crescent Chemical Ind",
                    CompanyId = 1006,
                    FinancialYearId = 4,
                    CreatedOn = DateTime.Now,
                    CreatedBy = "SystemSeed"
                };

                context.CommDetail.AddRange(det1, det2, det3, det4, det5);
                await context.SaveChangesAsync();
            }
        }
    }
}

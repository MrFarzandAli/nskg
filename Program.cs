using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nskg.Data;
using Nskg.Repositories;
using Nskg.Repositories.Interfaces;
using Nskg.Service;
using Nskg.Service.Interfaces;
using Nskg.Services;
using Nskg.Services.Interfaces;
using System.Text; // 👈 ADD THIS


var builder = WebApplication.CreateBuilder(args);
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// DB Connection
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        }));

// Identity Config
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>() // IMPORTANT for roles
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextService, UserContextService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<AccountingService, AccountingService>();
builder.Services.AddScoped<FinancialYearClosingService, FinancialYearClosingService>();
builder.Services.AddScoped<IRoleFormPermissionService, RoleFormPermissionService>();
// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}


using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbInitializer.SeedRoles(services);
}



app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<Nskg.Middleware.CompanyContextMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages();


app.Run();

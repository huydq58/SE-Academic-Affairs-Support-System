using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SE_Academic_Affairs_Support_System.Data;
using SE_Academic_Affairs_Support_System.Models;
using SE_Academic_Affairs_Support_System.Services;
using SE_Academic_Affairs_Support_System.Services.AccountManagement;
using SE_Academic_Affairs_Support_System.Services.AppRegistration;
using SE_Academic_Affairs_Support_System.Services.NotificationSevices;
using SE_Academic_Affairs_Support_System.Services.PeriodAutoClose;
using SE_Academic_Affairs_Support_System.Services.Email;
using SE_Academic_Affairs_Support_System.Services.EmailConfig;
using SE_Academic_Affairs_Support_System.Services.EmailNotification;
using SE_Academic_Affairs_Support_System.Services.ProjectRegistration;
using SE_Academic_Affairs_Support_System.Middleware;

namespace SE_Academic_Affairs_Support_System
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Data Protection — persist keys so encrypted passwords survive restarts
            var keysFolder = new DirectoryInfo(
                Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys"));
            keysFolder.Create();
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(keysFolder)
                .SetApplicationName("SE_Academic_Affairs_Support_System");

            // 2. Database
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("AzureConnection")));
            //builder.Services.AddDbContext<AppDbContext>(options =>
            //    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
            // 3. Services
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
            builder.Services.AddScoped<IEmailConfigurationService, EmailConfigurationService>();
            builder.Services.AddScoped<IAppRegistrationService, AppRegistrationService>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IRegistrationService, RegistrationService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IRegistrationPeriodStudentService, RegistrationPeriodStudentService>();
            builder.Services.AddHostedService<GradeSyncService>();
            builder.Services.AddHostedService<TopicSyncService>();
            builder.Services.AddHostedService<TopicCreateSyncService>();
            builder.Services.AddHostedService<PeriodAutoCloseService>();

            builder.Services.AddHttpClient<GoogleSheetsService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    });
            // 3. Identity
            builder.Services.AddIdentity<User, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();


            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Home/Index";      
                options.LogoutPath = "/Login/Logout";
                options.AccessDeniedPath = "/Home/Index"; 
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });

            builder.Services.AddControllersWithViews(); 

            var app = builder.Build();



            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage(); // outer: catches re-throws from ExceptionHandlingMiddleware
            }
            else
            {
                app.UseHsts();
            }

            app.UseMiddleware<ExceptionHandlingMiddleware>(); // inner: catches all unhandled exceptions first

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();  
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);

            // Seed admin account on startup if not exists
            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                if (!await roleManager.RoleExistsAsync("Admin"))
                    await roleManager.CreateAsync(new IdentityRole("Admin"));

                if (await userManager.FindByNameAsync("admin") == null)
                {
                    var adminUser = new User
                    {
                        UserName = "admin",
                        Email = "admin@localhost",
                        FullName = "Administrator",
                        Role = "Admin",
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    var result = await userManager.CreateAsync(adminUser, "a123123");
                    if (result.Succeeded)
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            app.Run();
        }

    }
}
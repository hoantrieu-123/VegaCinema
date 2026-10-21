using CinemaBooking.Business.Services;
using CinemaBooking.DataAccess;
using CinemaBooking.Domain;
using CinemaBooking.Integrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VegaCinema.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Configuration.AddEnvironmentVariables();
if (builder.Environment.IsDevelopment())
    builder.Services.AddDataProtection().SetApplicationName("VegaCinemas")
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")))
        .UseCustomCryptographicAlgorithms(new ManagedAuthenticatedEncryptorConfiguration
        { EncryptionAlgorithmType = typeof(System.Security.Cryptography.Aes), EncryptionAlgorithmKeySize = 256, ValidationAlgorithmType = typeof(System.Security.Cryptography.HMACSHA256) });
if (builder.Environment.IsEnvironment("Testing")) builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
var connection = builder.Configuration.GetConnectionString("VegaDatabase");
builder.Services.AddDbContext<VegaDbContext>(o => o.UseMySql(connection ?? "Server=127.0.0.1;Database=vega;User=unconfigured", new MySqlServerVersion(new Version(8, 4, 0))));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength = 8; o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<VegaDbContext>().AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/Denied";
    o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.Events.OnValidatePrincipal = async context =>
    {
        var user = await context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>().GetUserAsync(context.Principal!);
        if (user is null || !user.IsActive) { context.RejectPrincipal(); return; }
        await SecurityStampValidator.ValidatePrincipalAsync(context);
    };
});
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection("Booking"));
builder.Services.AddScoped<MovieService>(); builder.Services.AddScoped<CinemaService>();
builder.Services.AddScoped<ShowtimeService>(); builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<SeatHoldService>(); builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<TicketService>(); builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ReportService>(); builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<IQrCode, QrCodeService>();
builder.Services.AddSingleton<IPaymentGateway>(_ => new MockPaymentGateway(builder.Configuration["Payment:SigningKey"]
    ?? (builder.Environment.IsDevelopment() ? Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        : throw new InvalidOperationException("Cần cấu hình Payment:SigningKey qua biến môi trường hoặc User Secrets."))));
if (!string.IsNullOrWhiteSpace(connection)) builder.Services.AddHostedService<SeatExpiryWorker>();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff"; context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin"; await next();
});
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.UseMiddleware<BusinessExceptionMiddleware>();
if (string.IsNullOrWhiteSpace(connection))
{
    app.Use(async (context, next) => { if (!context.Request.Path.StartsWithSegments("/Setup")) { context.Response.Redirect("/Setup"); return; } await next(); });
}
else if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VegaDbContext>(); await db.Database.MigrateAsync();
    if (builder.Configuration.GetValue<bool>("Database:SeedDemo"))
        await SeedData.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
            builder.Configuration["Seed:DemoPassword"], builder.Configuration.GetValue<bool>("Seed:Large"));
}
app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
public partial class Program { }

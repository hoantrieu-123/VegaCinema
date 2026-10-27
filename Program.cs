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
using Turso.Data.Sqlite;

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
// var connection = builder.Configuration.GetConnectionString("VegaDatabase");
// builder.Services.AddDbContext<VegaDbContext>(o => o.UseMySql(connection ?? "Server=127.0.0.1;Database=vega;User=unconfigured", new MySqlServerVersion(new Version(8, 4, 0))));


var provider = builder.Configuration["Database:Provider"] ?? "MySql";

var mysqlConnection = builder.Configuration
    .GetConnectionString("VegaDatabase");

var sqliteConnection = builder.Configuration
    .GetConnectionString("SqliteDatabase")
    ?? "Data Source=vega-test.db";

var tursoUrl = builder.Configuration["TURSO_DATABASE_URL"];
var tursoToken = builder.Configuration["TURSO_AUTH_TOKEN"];

var isTurso = provider.Equals(
    "Turso", StringComparison.OrdinalIgnoreCase);

var isSqlite = provider.Equals(
    "Sqlite", StringComparison.OrdinalIgnoreCase);

var isMySql = provider.Equals(
    "MySql", StringComparison.OrdinalIgnoreCase);

if (!isTurso && !isSqlite && !isMySql)
{
    throw new InvalidOperationException(
        $"Database provider không được hỗ trợ: {provider}");
}

var hasDatabaseConnection = isTurso
    ? !string.IsNullOrWhiteSpace(tursoUrl) &&
      !string.IsNullOrWhiteSpace(tursoToken)
    : isSqlite
        ? !string.IsNullOrWhiteSpace(sqliteConnection)
        : !string.IsNullOrWhiteSpace(mysqlConnection);

if (isTurso && hasDatabaseConnection)
{
    builder.Services.AddScoped<SqliteConnection>(_ =>
        new SqliteConnection(
            $"Data Source={tursoUrl};Auth Token={tursoToken}"
        ));

    builder.Services.AddDbContext<VegaDbContext>(
        (serviceProvider, options) =>
        {
            var tursoConnection = serviceProvider
                .GetRequiredService<SqliteConnection>();

            options.UseSqlite(tursoConnection);
        });
}
else
{
    builder.Services.AddDbContext<VegaDbContext>(options =>
    {
        if (isSqlite)
        {
            options.UseSqlite(sqliteConnection);
        }
        else if (isMySql)
        {
            options.UseMySql(
                mysqlConnection ??
                    "Server=127.0.0.1;Database=vega;User=unconfigured",
                new MySqlServerVersion(new Version(8, 4, 0))
            );
        }
        else
        {
            // Chỉ dùng khi thiếu cấu hình Turso.
            // Middleware /Setup sẽ xử lý trường hợp này.
            options.UseSqlite("Data Source=:memory:");
        }
    });
}

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
if (hasDatabaseConnection &&
    provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<SeatExpiryWorker>();
}
var app = builder.Build();

Console.WriteLine($"[DATABASE] Provider: {provider}");

if (isTurso)
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<VegaDbContext>();

    var movieCount = await db.Movies.CountAsync();
    var userCount = await db.Users.CountAsync();

    Console.WriteLine("[TURSO] Connection successful");
    Console.WriteLine($"[TURSO] Movies: {movieCount}");
    Console.WriteLine($"[TURSO] Users: {userCount}");
}

if (isTurso &&
    builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    app.Logger.LogWarning(
        "AutoMigrate không được hỗ trợ cho Turso. " +
        "Schema phải được quản lý bằng script SQLite riêng.");
}
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff"; context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin"; await next();
});
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.UseMiddleware<BusinessExceptionMiddleware>();

if (!hasDatabaseConnection)
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments("/Setup"))
        {
            context.Response.Redirect("/Setup");
            return;
        }

        await next();
    });
}
else if (builder.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    // Chưa tự chạy migrations MySQL trên SQLite.
    if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<VegaDbContext>();

        await db.Database.MigrateAsync();

        if (builder.Configuration.GetValue<bool>("Database:SeedDemo"))
        {
            await SeedData.InitializeAsync(
                db,
                scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                builder.Configuration["Seed:DemoPassword"],
                builder.Configuration.GetValue<bool>("Seed:Large")
            );
        }
    }
}

app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
public partial class Program { }

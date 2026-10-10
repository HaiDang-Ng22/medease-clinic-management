using Healthy_System.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// Enable Npgsql legacy timestamp behavior for Supabase PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Connection Strings
var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "";

var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection")
    ?? "Data Source=HealthySystem.db";

// Quyết định dùng SQLite nếu:
// 1. DefaultConnection chứa [YOUR-PASSWORD] (chưa cấu hình)
// 2. DefaultConnection chứa "Data Source=" (đã override bởi appsettings.Development.json sang SQLite)
// 3. DefaultConnection rỗng
bool useSqlite = string.IsNullOrWhiteSpace(postgresConnectionString)
    || postgresConnectionString.Contains("[D@ng0799192226]")
    || postgresConnectionString.Contains("Data Source=HealthySystem.db");

string activeConnectionString = useSqlite ? sqliteConnectionString : postgresConnectionString;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(activeConnectionString);
    }
    else
    {
        options.UseNpgsql(activeConnectionString);
    }
});

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Database auto migration & seed
try
{
    if (useSqlite)
    {
        // Đối với SQLite: dùng EnsureCreated để tạo schema mới nhất nếu chưa có
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DbInitializer.EnsureDatabaseSchemaCreated(context);
        DbInitializer.SeedData(context);
    }
    else
    {
        // Đối với Postgres: chạy migration
        DbInitializer.Initialize(app.Services);
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "[DB] Lỗi khi khởi tạo database: {Message}", ex.Message);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

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
    ?? "Host=aws-0-ap-northeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.recelqensesdpwzdvslp;Password=[YOUR-PASSWORD];Pooling=true;SSL Mode=Require;Trust Server Certificate=true";

var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection") 
    ?? "Data Source=HealthySystem.db";

// Database Configuration:
// If [YOUR-PASSWORD] is still placeholder, gracefully fallback to SQLite for immediate testing
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (postgresConnectionString.Contains("[YOUR-PASSWORD]") || postgresConnectionString.Contains("YOUR-PASSWORD"))
    {
        options.UseSqlite(sqliteConnectionString);
    }
    else
    {
        options.UseNpgsql(postgresConnectionString);
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
    DbInitializer.Initialize(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning($"Khởi tạo DB ban đầu gặp thông báo ({ex.Message}). Đang sử dụng cơ sở dữ liệu dự phòng SQLite...");

    using var scope = app.Services.CreateScope();
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
    optionsBuilder.UseSqlite(sqliteConnectionString);
    using var sqliteContext = new AppDbContext(optionsBuilder.Options);
    DbInitializer.EnsureDatabaseSchemaCreated(sqliteContext);
    DbInitializer.SeedData(sqliteContext);
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

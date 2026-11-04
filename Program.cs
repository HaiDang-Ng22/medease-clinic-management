using Healthy_System.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

// Enable Npgsql legacy timestamp behavior for Supabase PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("--seed-supabase"))
{
    var postgresConn = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine("[SUPABASE] Connecting to Supabase: " + postgresConn);
    var optBuilder = new DbContextOptionsBuilder<AppDbContext>();
    optBuilder.UseNpgsql(postgresConn);
    optBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    using var db = new AppDbContext(optBuilder.Options);
    Console.WriteLine("[SUPABASE] Generating PostgreSQL DDL Script...");
    var script = db.Database.GenerateCreateScript();
    Console.WriteLine($"[SUPABASE] Script length: {script.Length} chars. Executing on Supabase...");
    db.Database.ExecuteSqlRaw(script);
    Console.WriteLine("[SUPABASE] Tables created successfully! Seeding data...");
    DbInitializer.SeedData(db);
    Console.WriteLine(">>> [SUPABASE] HOAN TAT TAO BANG VA NAP DU LIEU MAU LEN SUPABASE! <<<");
    return;
}

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // US41: tự động ghi Audit Log cho mọi thao tác thay đổi dữ liệu
    options.Filters.Add<Healthy_System.Filters.AuditLogActionFilter>();
});

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
    || postgresConnectionString.Contains("[YOUR-PASSWORD]")

    || postgresConnectionString.Contains("[D@ng0799192226]")
    || postgresConnectionString.Contains("Data Source=HealthySystem.db");

string activeConnectionString = useSqlite ? sqliteConnectionString : postgresConnectionString;

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
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
        // Đối với Postgres: đã tạo schema DDL, chạy seed data nếu cần
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DbInitializer.SeedData(context);
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

using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CivicService.Data;
using CivicService.Models;
using CivicService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ===========================================
// Helper: Get config from multiple sources
// ===========================================
string? GetConfig(params string[] keys)
{
    foreach (var key in keys)
    {
        // Try ASP.NET Core configuration first
        var value = builder.Configuration[key];
        if (!string.IsNullOrWhiteSpace(value)) return value;

        // Try environment variable directly (for Railway-style vars like JWT_KEY)
        value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) return value;

        // Try with underscores replaced (JWT_KEY -> Jwt__Key style)
        var envKey = key.Replace(":", "__");
        value = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(value)) return value;
    }
    return null;
}

// ===========================================
// Helper: Convert DATABASE_URL to Npgsql format
// ===========================================
// Railway provides: postgresql://user:pass@host:5432/dbname
// Npgsql expects:   Host=host;Port=5432;Database=dbname;Username=user;Password=pass
string ConvertDatabaseUrl(string databaseUrl)
{
    if (!databaseUrl.StartsWith("postgres://") && !databaseUrl.StartsWith("postgresql://"))
        return databaseUrl; // Already in Npgsql format

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo.Length > 0 ? userInfo[0] : "";
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var host = uri.Host;
    var port = uri.Port > 0 ? uri.Port : 5432;
    var database = uri.AbsolutePath.TrimStart('/');

    var npgsqlConnectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

    // Add SSL for Railway (they require it)
    if (!databaseUrl.Contains("sslmode=", StringComparison.OrdinalIgnoreCase))
        npgsqlConnectionString += ";SSL Mode=Require;Trust Server Certificate=true";

    return npgsqlConnectionString;
}

// ===========================================
// Validate Required Configuration
// ===========================================

// JWT Key - check multiple naming conventions
var jwtKey = GetConfig("Jwt:Key", "JWT_KEY", "JwtKey");
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Configuration error: JWT Key is required. Set 'Jwt__Key' or 'JWT_KEY' environment variable.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Configuration error: JWT Key must be at least 32 characters for security.");

// Admin credentials
var adminEmail = GetConfig("DefaultAdmin:Email", "DEFAULT_ADMIN_EMAIL", "AdminEmail");
var adminPassword = GetConfig("DefaultAdmin:Password", "DEFAULT_ADMIN_PASSWORD", "AdminPassword");
if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    throw new InvalidOperationException("Configuration error: Admin credentials required. Set 'DefaultAdmin__Email'/'DefaultAdmin__Password' or 'DEFAULT_ADMIN_EMAIL'/'DEFAULT_ADMIN_PASSWORD' environment variables.");

// Database connection
var dbProvider = GetConfig("DatabaseProvider", "DATABASE_PROVIDER") ?? "Postgres";
var connectionString = builder.Configuration.GetConnectionString(dbProvider)
    ?? GetConfig("DATABASE_URL", $"ConnectionStrings:{dbProvider}");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException($"Configuration error: Database connection required. Set 'DATABASE_URL' or 'ConnectionStrings__{dbProvider}' environment variable.");

// Convert DATABASE_URL format if needed
if (dbProvider == "Postgres")
    connectionString = ConvertDatabaseUrl(connectionString);

// JWT settings with defaults
var jwtIssuer = GetConfig("Jwt:Issuer", "JWT_ISSUER") ?? "CivicService";
var jwtAudience = GetConfig("Jwt:Audience", "JWT_AUDIENCE") ?? "CivicServiceUsers";

// Debug output
Console.WriteLine($"[Config] Database Provider: {dbProvider}");
Console.WriteLine($"[Config] Connection String Format: {(connectionString.StartsWith("Host=") ? "Npgsql" : "URI")}");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

// Configure Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global rate limit: 100 requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Strict limit for submission endpoints: 5 requests per minute per IP
    options.AddPolicy("submissions", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Auth endpoints: 10 attempts per 15 minutes per IP (prevent brute force)
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // Upvote endpoints: 30 per minute per IP
    options.AddPolicy("upvotes", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// register services
builder.Services.AddHttpClient<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// setup database
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider == "Postgres")
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // User settings
    options.User.RequireUniqueEmail = true;

    // Lockout settings (brute force protection)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["db", "sql"]);

var app = builder.Build();

// Apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Apply pending migrations (creates tables if they don't exist)
    // Skip for InMemory database (used in tests)
    var isInMemory = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    if (!isInMemory)
    {
        Console.WriteLine("[Startup] Applying database migrations...");
        await db.Database.MigrateAsync();
        Console.WriteLine("[Startup] Migrations applied successfully.");
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    // Create roles if they don't exist
    foreach (var role in AppRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Create default admin if no admin exists
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Production security settings
    // HSTS - tell browsers to only use HTTPS for 1 year
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseStaticFiles();

// Rate limiting middleware
app.UseRateLimiter();

// Authentication & Authorization middleware (order matters!)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Simple liveness probe (no dependencies)
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapFallbackToFile("index.html");

app.Run();

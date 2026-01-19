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
// Debug Environment Variables (Railway)
// ===========================================
Console.WriteLine("=== Configuration Debug Info ===");
Console.WriteLine($"DATABASE_URL exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"))}");
Console.WriteLine($"ConnectionStrings__Postgres exists: {!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres"))}");
Console.WriteLine($"DatabaseProvider: {Environment.GetEnvironmentVariable("DatabaseProvider")}");

// ===========================================
// Validate Required Configuration
// ===========================================
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Configuration error: 'Jwt:Key' is required. Set it via environment variable 'Jwt__Key' or in appsettings.json.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Configuration error: 'Jwt:Key' must be at least 32 characters for security.");

var adminEmail = builder.Configuration["DefaultAdmin:Email"];
var adminPassword = builder.Configuration["DefaultAdmin:Password"];
if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    throw new InvalidOperationException("Configuration error: 'DefaultAdmin:Email' and 'DefaultAdmin:Password' are required. Set them via environment variables or in appsettings.json.");

var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString(dbProvider);

// Fallback to DATABASE_URL if connection string is empty (Railway default)
if (string.IsNullOrWhiteSpace(connectionString) && dbProvider == "Postgres")
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        Console.WriteLine("Using DATABASE_URL as fallback");
        connectionString = databaseUrl;
    }
}

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException($"Configuration error: Connection string for '{dbProvider}' is required. Set 'ConnectionStrings__{dbProvider}' or 'DATABASE_URL' environment variable.");

// Log connection string info for debugging (without exposing password)
Console.WriteLine($"Database Provider: {dbProvider}");
Console.WriteLine($"Connection String Length: {connectionString?.Length ?? 0}");
Console.WriteLine($"Connection String First 30 chars: {(connectionString?.Length > 30 ? connectionString.Substring(0, 30) + "..." : connectionString)}");

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CivicService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CivicServiceUsers";

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

// Seed roles on startup
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

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

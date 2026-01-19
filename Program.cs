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


string? GetConfig(params string[] keys)
{
    foreach (var key in keys)
    {
        var value = builder.Configuration[key];
        if (!string.IsNullOrWhiteSpace(value)) return value;

        value = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(value)) return value;

        var envKey = key.Replace(":", "__");
        value = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(value)) return value;
    }
    return null;
}

string ConvertToNpgsqlFormat(string databaseUrl)
{
    if (!databaseUrl.StartsWith("postgres://") && !databaseUrl.StartsWith("postgresql://"))
        return databaseUrl;

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var username = userInfo.Length > 0 ? userInfo[0] : "";
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";

    return BuildConnectionString(uri.Host, uri.Port > 0 ? uri.Port : 5432, uri.AbsolutePath.TrimStart('/'), username, password, databaseUrl);
}

string BuildConnectionString(string host, int port, string database, string username, string password, string originalUrl)
{
    var connStr = $"Host={host};Port={port};Database={database};Username={username};Password={password}";

    if (!originalUrl.Contains("sslmode=", StringComparison.OrdinalIgnoreCase))
        connStr += ";SSL Mode=Require;Trust Server Certificate=true";

    return connStr;
}


var jwtKey = GetConfig("Jwt:Key", "JWT_KEY", "JwtKey");
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Configuration error: JWT Key is required. Set 'Jwt__Key' or 'JWT_KEY' environment variable.");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Configuration error: JWT Key must be at least 32 characters for security.");

var adminEmail = GetConfig("DefaultAdmin:Email", "DEFAULT_ADMIN_EMAIL", "AdminEmail");
var adminPassword = GetConfig("DefaultAdmin:Password", "DEFAULT_ADMIN_PASSWORD", "AdminPassword");
if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
    throw new InvalidOperationException("Configuration error: Admin credentials required. Set 'DefaultAdmin__Email'/'DefaultAdmin__Password' or 'DEFAULT_ADMIN_EMAIL'/'DEFAULT_ADMIN_PASSWORD' environment variables.");


var dbProvider = GetConfig("DatabaseProvider", "DATABASE_PROVIDER") ?? "Postgres";
var connectionString = builder.Configuration.GetConnectionString(dbProvider)
    ?? GetConfig("DATABASE_URL", $"ConnectionStrings:{dbProvider}");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException($"Configuration error: Database connection required. Set 'DATABASE_URL' or 'ConnectionStrings__{dbProvider}' environment variable.");

if (dbProvider == "Postgres")
    connectionString = ConvertToNpgsqlFormat(connectionString);

var jwtIssuer = GetConfig("Jwt:Issuer", "JWT_ISSUER") ?? "CivicService";
var jwtAudience = GetConfig("Jwt:Audience", "JWT_AUDIENCE") ?? "CivicServiceUsers";

builder.Configuration["Jwt:Key"] = jwtKey;
builder.Configuration["Jwt:Issuer"] = jwtIssuer;
builder.Configuration["Jwt:Audience"] = jwtAudience;

Console.WriteLine($"[Config] Database Provider: {dbProvider}");
Console.WriteLine($"[Config] JWT Key Length: {jwtKey.Length}");
Console.WriteLine($"[Config] Connection String Format: {(connectionString.StartsWith("Host=") ? "Npgsql" : "URI")}");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

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

    options.AddPolicy("upvotes", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddHttpClient<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


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

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

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

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["db", "sql"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var isInMemory = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    if (!isInMemory)
    {
        var resetDb = Environment.GetEnvironmentVariable("RESET_DATABASE");
        if (resetDb?.ToLower() == "true")
        {
            Console.WriteLine("[Startup] RESET_DATABASE=true detected. Dropping and recreating database...");
            await db.Database.EnsureDeletedAsync();
            Console.WriteLine("[Startup] Database dropped.");
        }

        Console.WriteLine("[Startup] Applying database migrations...");
        await db.Database.MigrateAsync();
        Console.WriteLine("[Startup] Migrations applied successfully.");
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    foreach (var role in AppRoles.All)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

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
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.MapFallbackToFile("index.html");

app.Run();

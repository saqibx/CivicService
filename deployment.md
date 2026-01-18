# CivicService Deployment Specifications

## Project Overview

| Property | Value |
|----------|-------|
| Application Name | CivicService |
| Type | ASP.NET Core Web API + SPA |
| Framework | .NET 10.0 |
| Architecture | Monolithic with static frontend |

---

## Runtime Requirements

| Requirement | Specification |
|-------------|---------------|
| Runtime | .NET 10.0 |
| OS | Linux (recommended) or Windows |
| Architecture | x64 or ARM64 |

---

## Environment Variables / App Settings

### Required Configuration

| Key | Description | Example | Secret |
|-----|-------------|---------|--------|
| `Jwt:Key` | JWT signing key (min 32 chars) | `YourSecureKeyHere123!@#$%` | Yes |
| `Jwt:Issuer` | JWT token issuer | `CivicService` | No |
| `Jwt:Audience` | JWT token audience | `CivicServiceUsers` | No |
| `DefaultAdmin:Email` | Initial admin account email | `admin@yourdomain.com` | Yes |
| `DefaultAdmin:Password` | Initial admin account password | `SecurePassword123!` | Yes |
| `DatabaseProvider` | Database provider to use | `Sqlite` or `Postgres` | No |
| `ConnectionStrings:Sqlite` | SQLite connection string | `Data Source=civicservice.db` | No |
| `ConnectionStrings:Postgres` | PostgreSQL connection string | See below | Yes |

### Optional Configuration

| Key | Description | Default | Secret |
|-----|-------------|---------|--------|
| `ReCaptcha:SiteKey` | Google reCAPTCHA v3 site key | (empty - disabled) | No |
| `ReCaptcha:SecretKey` | Google reCAPTCHA v3 secret | (empty - disabled) | Yes |
| `ReCaptcha:MinScore` | Minimum CAPTCHA score (0.0-1.0) | `0.5` | No |
| `Smtp:Host` | SMTP server hostname | (empty - disabled) | No |
| `Smtp:Port` | SMTP server port | `587` | No |
| `Smtp:UseSsl` | Enable SSL/TLS | `false` | No |
| `Smtp:Username` | SMTP authentication username | (empty) | Yes |
| `Smtp:Password` | SMTP authentication password | (empty) | Yes |
| `Smtp:FromEmail` | Sender email address | `noreply@civicservice.local` | No |
| `Smtp:FromName` | Sender display name | `Civic Service Portal` | No |

### PostgreSQL Connection String Format
```
Host=your-server.postgres.database.azure.com;Database=civicservice;Username=your-user;Password=your-password;SSL Mode=Require;Trust Server Certificate=true
```

---

## Database

### Supported Providers
- **SQLite** (default, development)
- **PostgreSQL** (recommended for production)

### Schema
Entity Framework Core Code-First migrations. Run migrations on deployment:
```bash
dotnet ef database update
```

### Tables Created
- `AspNetUsers` - User accounts (ASP.NET Identity)
- `AspNetRoles` - User roles (Admin, Staff, User)
- `AspNetUserRoles` - User-role mappings
- `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens` - Identity support tables
- `AspNetRoleClaims` - Role claims
- `ServiceRequests` - Main service request data
- `Upvotes` - User/IP upvotes on requests

### Seeded Data
On first startup, the application automatically creates:
- Roles: `Admin`, `Staff`, `User`
- Default admin account (from `DefaultAdmin:Email` and `DefaultAdmin:Password`)

---

## API Endpoints

### Public Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/requests` | List service requests (paginated, filterable) |
| GET | `/api/requests/{id}` | Get single request details |
| POST | `/api/requests` | Create new service request |
| POST | `/api/requests/{id}/upvote` | Upvote a request |
| DELETE | `/api/requests/{id}/upvote` | Remove upvote |
| POST | `/api/auth/register` | Register new user |
| POST | `/api/auth/login` | Authenticate user |

### Authenticated Endpoints (Requires JWT)
| Method | Path | Description | Role |
|--------|------|-------------|------|
| GET | `/api/auth/me` | Get current user profile | Any |
| GET | `/api/requests/my` | Get user's own requests | Any |

### Admin Endpoints (Requires Admin Role)
| Method | Path | Description |
|--------|------|-------------|
| PUT | `/api/requests/{id}/status` | Update request status |
| GET | `/api/requests/stats` | Get dashboard statistics |
| POST | `/api/auth/staff` | Create staff account |
| GET | `/api/auth/users` | List all users |

### Health Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Full health check (includes DB) |
| GET | `/health/live` | Simple liveness probe |

---

## Health Checks

### `/health` Response Format
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "duration": 45.23,
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "duration": 12.5,
      "description": null,
      "exception": null
    }
  ]
}
```

### Azure Configuration
- **Liveness Probe**: `/health/live`
- **Readiness Probe**: `/health`
- **Startup Probe**: `/health` (with longer timeout for DB migrations)

---

## Rate Limiting

| Policy | Limit | Window | Applied To |
|--------|-------|--------|------------|
| Global | 100 requests | 1 minute | All endpoints |
| Submissions | 5 requests | 1 minute | POST `/api/requests` |
| Auth | 10 requests | 15 minutes | `/api/auth/*` |
| Upvotes | 30 requests | 1 minute | Upvote endpoints |

Rate limiting is based on client IP address. Returns `429 Too Many Requests` when exceeded.

---

## Security Features

### Enabled by Default
- HTTPS redirection
- HSTS (production only, 1 year)
- JWT Bearer authentication
- Role-based authorization
- Account lockout (5 failed attempts = 15 min lockout)
- Rate limiting per IP
- Security headers:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `X-XSS-Protection: 1; mode=block`
  - `Referrer-Policy: strict-origin-when-cross-origin`

### Password Requirements
- Minimum 6 characters
- At least 1 digit
- At least 1 lowercase letter

---

## Static Files

The application serves a static SPA from `/wwwroot`:

| File | Purpose |
|------|---------|
| `index.html` | Main dashboard (public) |
| `map.html` | Map view |
| `my-requests.html` | User's requests (authenticated) |
| `admin.html` | Admin dashboard |
| `css/styles.css` | Stylesheet |
| `js/app.js` | Main application logic |
| `js/auth.js` | Authentication logic |
| `js/config.js` | Frontend configuration |
| `js/places.js` | Google Places integration |

Fallback: All unmatched routes serve `index.html` (SPA routing).

---

## External Dependencies

| Service | Purpose | Required |
|---------|---------|----------|
| PostgreSQL | Production database | Yes (or SQLite) |
| Google reCAPTCHA v3 | Bot protection | No |
| SMTP Server | Email notifications | No |
| Google Maps/Places API | Address autocomplete (frontend) | No |

---

## Azure Resource Recommendations

### App Service
| Setting | Recommendation |
|---------|----------------|
| SKU | B1 (Basic) minimum, S1 (Standard) recommended |
| OS | Linux |
| Runtime | .NET 10 |
| Always On | Enabled (prevents cold starts) |

### Azure Database for PostgreSQL
| Setting | Recommendation |
|---------|----------------|
| SKU | Burstable B1ms (dev) or General Purpose D2s_v3 (prod) |
| Version | PostgreSQL 16 |
| Storage | 32 GB minimum |
| Backup | Geo-redundant (production) |

### Azure Key Vault (Recommended)
Store these secrets:
- `Jwt--Key`
- `DefaultAdmin--Email`
- `DefaultAdmin--Password`
- `ConnectionStrings--Postgres`
- `ReCaptcha--SecretKey`
- `Smtp--Username`
- `Smtp--Password`

---

## Build & Publish

### Build Command
```bash
dotnet publish -c Release -o ./publish
```

### Output
Self-contained deployment not required; framework-dependent deployment recommended.

### Publish Artifacts
- `CivicService.dll` - Main application
- `wwwroot/` - Static frontend files
- `appsettings.json` - Configuration template
- `web.config` - IIS configuration (Windows only)

---

## Logging

Default configuration writes to console (stdout). Azure App Service captures these automatically.

| Category | Level |
|----------|-------|
| Default | Information |
| Microsoft.AspNetCore | Warning |

For production, consider adding Application Insights:
```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

---

## CORS

Not configured by default. If deploying frontend separately, add CORS configuration:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://your-frontend-domain.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

---

## Migration Checklist

- [ ] Set `Jwt:Key` to a secure random string (32+ characters)
- [ ] Set `DefaultAdmin:Email` and `DefaultAdmin:Password`
- [ ] Configure database connection string
- [ ] Run database migrations
- [ ] Configure health check probes
- [ ] Enable HTTPS/TLS certificate
- [ ] (Optional) Configure reCAPTCHA
- [ ] (Optional) Configure SMTP for emails
- [ ] (Optional) Set up Application Insights

# Git Setup for CivicService

## 1. Create .gitignore

Create a `.gitignore` file with standard .NET ignores:

```bash
dotnet new gitignore
```

Or manually add these essentials:

```
# Build outputs
bin/
obj/

# User-specific files
*.user
*.suo
*.userosscache
*.sln.docstates

# IDE
.idea/
.vs/
*.swp

# SQLite database (don't commit local dev data)
*.db
*.db-shm
*.db-wal

# Environment/secrets
appsettings.Development.json
appsettings.*.local.json
.env

# macOS
.DS_Store
```

## 2. Initialize Git

```bash
cd /Users/saqib/CSharpProjects/HelloDotnet
git init
```

## 3. (Optional) Rename the folder

The folder is still named `HelloDotnet`. To rename it:

```bash
cd ..
mv HelloDotnet CivicService
cd CivicService
```

## 4. Add and commit

```bash
git add .
git commit -m "Initial commit: CivicService 311 backend with EF Core"
```

## 5. Push to remote

```bash
# Create repo on GitHub/GitLab first, then:
git remote add origin git@github.com:YOUR_USERNAME/CivicService.git
git branch -M main
git push -u origin main
```

## Files to NOT commit

| File | Reason |
|------|--------|
| `*.db` | Local SQLite database |
| `appsettings.Development.json` | May contain local secrets |
| `bin/`, `obj/` | Build artifacts |

## Files that ARE safe to commit

| File | Reason |
|------|--------|
| `appsettings.json` | Contains defaults (no real secrets for SQLite) |
| `Migrations/` | Required to recreate database schema |
| `*.csproj` | Project configuration |

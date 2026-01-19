# CivicService

CivicService is a web application built with C# and web technologies that helps citizens discover, request, and track civic services and community resources. It provides a simple interface for submitting service requests, viewing request status, and for administrators to manage and respond to requests.

This repository contains the backend (C# ASP.NET) and frontend (HTML, JavaScript, CSS) components, with optional Docker support for local development and deployment.

## Table of Contents

- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Repository Structure](#repository-structure)
- [Prerequisites](#prerequisites)
- [Getting Started (Local Development)](#getting-started-local-development)
- [Configuration & Environment Variables](#configuration--environment-variables)
- [Running with Docker](#running-with-docker)
- [Testing](#testing)
- [Building & Publishing](#building--publishing)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Key Features

- Submit and track civic service requests (e.g., potholes, streetlight outages).
- Admin dashboard for triaging, assigning, and resolving requests.
- Simple, responsive UI for citizens and admins.
- RESTful API endpoints for integrations and mobile clients.
- Dockerfile provided for containerized deployment.

## Tech Stack

- Backend: C# (.NET 6+ / ASP.NET Core)
- Frontend: HTML, JavaScript, CSS (server-rendered or SPA depending on project layout)
- Database: (e.g., PostgreSQL, SQL Server) — configurable via connection string
- Containerization: Docker

## Repository Structure

(Adjust paths to match the actual layout of your repo.)

- /src/ — C# backend project(s)
- /src/ClientApp/ — frontend assets (HTML/CSS/JS) or SPA
- /docker/ or Dockerfile — containerization config
- /docs/ — design and architecture docs
- /tests/ — unit and integration tests

## Prerequisites

- .NET SDK 6.0 or later (install from https://dotnet.microsoft.com/)
- Node.js and npm (if frontend build tooling is used)
- Docker (optional, for container builds)
- A relational database (Postgres/SQL Server) or use an in-memory provider for development

## Getting Started (Local Development)

1. Clone the repository
   ```bash
   git clone https://github.com/saqibx/CivicService.git
   cd CivicService
   ```

2. Set up environment variables (see [Configuration](#configuration--environment-variables)).

3. Restore and build the backend
   ```bash
   cd src
   dotnet restore
   dotnet build
   ```

4. If the frontend is a separate project (ClientApp), install dependencies and run the dev server
   ```bash
   cd src/ClientApp
   npm install
   npm run dev
   ```

5. Run the backend
   ```bash
   cd ../../src/YourBackendProject
   dotnet run
   ```
   The API will typically be available at http://localhost:5000 (or as configured).

## Configuration & Environment Variables

The application reads configuration from appsettings.json and environment variables. Common variables:

- `ASPNETCORE_ENVIRONMENT` — Development / Production
- `ConnectionStrings__Default` — Database connection string
- `JWT__Key` — Secret for JWT token signing (if authentication is used)
- `ASPNETCORE_URLS` — e.g., `http://*:5000`

Create an `appsettings.Development.json` for local overrides and add secrets to a secure store or `.env` file (do not commit secrets).

Example `.env` (for local development):
```
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__Default=Server=localhost;Database=civicservice_dev;User Id=sa;Password=yourStrong(!)Password;
JWT__Key=ChangeThisToASecretKeyForDev
```

## Running with Docker

Build and run using Docker:

1. Build the image
   ```bash
   docker build -t civicservice:local .
   ```

2. Run the container
   ```bash
   docker run -e "ConnectionStrings__Default=..." -p 5000:80 civicservice:local
   ```

3. For a compose setup (if a docker-compose.yml exists), use:
   ```bash
   docker-compose up --build
   ```

Adjust port mappings, volumes, and environment variables as needed.

## Testing

Unit and integration tests can be run with the dotnet test command:

```bash
cd tests
dotnet test
```

Aim to add tests for API controllers, services, and core business logic.

## Building & Publishing

To publish the backend for production:

```bash
cd src/YourBackendProject
dotnet publish -c Release -o ./publish
```

Then build a production Docker image referencing the published files.

CI/CD: Consider GitHub Actions to build, test, and push Docker images on merges to main.


## Roadmap / Ideas

- User authentication and role-based access (citizen, admin, operator)
- Email / SMS notifications on status updates
- Geo-location and map view of requests
- Analytics dashboard for request trends
- External integrations (municipal systems, Open311)



## Contact

Maintainer: saqibx
Repository: https://github.com/saqibx/CivicService

```

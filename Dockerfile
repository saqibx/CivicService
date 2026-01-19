# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY CivicService.csproj ./
RUN dotnet restore ./CivicService.csproj

# Copy the rest of the repo
COPY . ./


RUN echo "=== DEBUG: /src listing ===" \
 && ls -la \
 && echo "=== DEBUG: .cs count ===" \
 && find . -name "*.cs" | wc -l \
 && echo "=== DEBUG: .razor count ===" \
 && find . -name "*.razor" | wc -l \
 && echo "=== DEBUG: .cshtml count ===" \
 && find . -name "*.cshtml" | wc -l \
 && echo "=== DEBUG: first 30 .cs files ===" \
 && find . -name "*.cs" | head -n 30





# Publish the main app explicitly (NOT the solution)
RUN dotnet publish ./CivicService.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CivicService.dll"]

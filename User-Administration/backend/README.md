# PINÈDE IDENTITY - Backend API

This is the backend API for the PINÈDE IDENTITY IAM platform, built with .NET 8 and Clean Architecture.

## Solution Structure

```
backend/
├── src/
│   ├── Nexorsys.Identity.Core/      # Domain entities, interfaces, DTOs
│   ├── Nexorsys.Identity.Infrastructure/ # EF Core, LDAP, JWT services
│   └── Nexorsys.Identity.API/       # Controllers, middleware, API endpoints
├── Nexorsys.Identity.Solution.sln
└── .gitignore
```

## Prerequisites

- .NET 8 SDK
- PostgreSQL 14+
- Node.js 18+ (for frontend)

## Configuration

### appsettings.json

Configure database connection, JWT settings, and CORS:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=NexorSys_Dev;Username=nexorsys_dev;Password=<from-secret-provider>"
  },
  "Jwt": {
    "Key": "<from-secret-provider>",
    "Issuer": "NexorSys.Identity",
    "Audience": "NexorSys.Identity.Clients"
  },
  "CORS": {
    "AllowedOrigins": "http://localhost:3000,https://identity.example.invalid,https://kiosk.example.invalid"
  }
}
```

## Development

### Running the API

```bash
cd backend/src/Nexorsys.Identity.API
dotnet run
```

The API will be available at:
- https://localhost:7000 (HTTPS)
- http://localhost:5000 (HTTP)

### Database Migrations

When EF Core tools are available:

```bash
cd backend/src/Nexorsys.Identity.Infrastructure
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Architecture

The solution follows Clean Architecture principles:

- **Core**: Contains domain entities, interfaces, and DTOs (independent of frameworks)
- **Infrastructure**: Implements persistence (EF Core), external services (LDAP), and infrastructure concerns
- **API**: Contains controllers, middleware, and application-specific logic

## Security

- JWT authentication with BCrypt-hashed PINs
- RBAC policies (to be implemented)
- Input validation
- CORS configuration

## Testing

Run unit tests:

```bash
cd backend/src/Nexorsys.Identity.API
dotnet test
```

## Docker

Build and run with Docker:

```bash
docker-compose up --build
```

This will start:
- PostgreSQL database
- Backend API
- Frontend development server

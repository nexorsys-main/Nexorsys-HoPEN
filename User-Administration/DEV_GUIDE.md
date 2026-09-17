# DEV_GUIDE.md - Commandes et Configuration du Projet

## Frontend (React + Vite)

### Development
- `cd frontend/nexorsys-identity-ui && npm run dev` - Start development server
- Listens on http://localhost:3000

### Type Checking
- `cd frontend/nexorsys-identity-ui && npm run typecheck` - Run TypeScript type checking

### Build
- `cd frontend/nexorsys-identity-ui && npm run build` - Production build

## Backend (.NET 8 - when SDK is installed)

### Solution Structure
- `backend/src/Nexorsys.Identity.Core/` - Domain entities, interfaces, DTOs
- `backend/src/Nexorsys.Identity.Infrastructure/` - EF Core, LDAP, JWT services
- `backend/src/Nexorsys.Identity.API/` - Controllers, middleware, API endpoints

### Commands (to be added when .NET SDK is available)
- `cd backend && dotnet restore` - Restore NuGet packages
- `cd backend && dotnet build` - Build solution
- `cd backend/src/Nexorsys.Identity.API && dotnet run` - Run API (on https://localhost:7000, http://localhost:5000)
- `cd backend/src/Nexorsys.Identity.API && dotnet test` - Run unit tests
- `cd backend/src/Nexorsys.Identity.API && dotnet ef database update` - Update database

## Database

### PostgreSQL
- Ensure PostgreSQL 14+ is running
- Database configuration: provide `NEXORSYS_DATABASE` or `NEXORSYS_DB_PASSWORD` through the local secret provider; do not place credentials in source files.

### Migration Commands (when EF Core tools available)
- `cd backend/src/Nexorsys.Identity.Infrastructure && dotnet ef migrations add InitialCreate`
- `cd backend/src/Nexorsys.Identity.Infrastructure && dotnet ef database update`

## Linting and Formatting

### Frontend
- ESLint: `cd frontend/nexorsys-identity-ui && npx eslint "src/**/*.{js,ts,tsx}"`
- Prettier: `cd frontend/nexorsys-identity-ui && npx prettier --write "src/**/*.{js,ts,tsx}"`

### Backend
- When .NET SDK is available, add:
  - `dotnet format` for code formatting
  - `dotnet analyze` for code analysis

## Testing

### Frontend
- `cd frontend/nexorsys-identity-ui && npm test` (Jest/React Testing Library)

### Backend
- `cd backend/src/Nexorsys.Identity.API && dotnet test` (xUnit/NUnit)

## Docker (optional)

### Build and run with Docker
- `docker-compose up` (when docker-compose.yml is created)

## Security Notes

- Never commit secrets to repository
- Use environment variables for connection strings and keys
- PINs are hashed with BCrypt (use `BCrypt.Net-Next` library)
- JWT tokens should be signed with a secure secret

## Project Structure

```
Clinique/User-Administration/
├── db/
│   └── init_db.sql          # Database schema and seed data
├── backend/
│   ├── src/                 # .NET projects
│   │   ├── Nexorsys.Identity.Core/
│   │   ├── Nexorsys.Identity.Infrastructure/
│   │   └── Nexorsys.Identity.API/
│   ├── Nexorsys.Identity.Solution.sln
│   └── .gitignore
├── frontend/
│   └── nexorsys-identity-ui/
│       ├── src/
│       ├── public/
│       ├── package.json
│       ├── tailwind.config.js
│       ├── tsconfig.json
│       ├── vite.config.ts
│       └── .gitignore
└── AGENTS.md                # This file
```

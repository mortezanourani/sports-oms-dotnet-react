# Sports OMS (.NET + React)

**Sports Organizational Management System** — a full-stack platform for managing sports facilities, athlete statistics, champions and medals, and federation leadership records across provinces and cities, with a role-based dashboard and an internal messaging/reporting system.

Built with **ASP.NET Core Web API** (backend) and **React** (frontend). The interface is in **Persian (RTL)**.

---

## Overview

Sports OMS replaces a single-city, monolithic organizational-management system with a leaner, country-wide platform focused specifically on:

- **Sports facilities** — government, private, and organization-owned (MSY) facilities, along with their contracts and licenses
- **Athlete statistics** — monthly registered-athlete counts by gender, federation, province, and city
- **Champions & medals** — a full career history per champion: every medal, competition, age group, and level they've ever won
- **Federations** — including full historical tracking of who held each federation's President / Vice / Women's Vice position and when
- **Role-based dashboard access** — from national administrators down to city-level officers, each scoped to see and edit only the data relevant to their role and geography
- **Internal messaging** — users can send reports and messages to one another, including file attachments

Public-facing pages expose read-only statistics (facilities, champions, athlete counts) with no login required. Everything else lives behind an authenticated, role- and geography-scoped dashboard.

---

## Tech Stack

**Backend**
- ASP.NET Core Web API (.NET 8)
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity + JWT Bearer authentication

**Frontend**
- React (Vite)
- Ant Design (RTL-first component library)
- TanStack Query (React Query) for server state
- React Router

---

## Architecture Highlights

- **Role-based + geography-scoped authorization.** Every user has a fixed role (Admin, Manager, and several specialized officer roles) and a geographic scope (National, Province, or City). API endpoints filter and validate data access against both dimensions on every request — scope restrictions are enforced server-side, never assumed from the UI alone.
- **Historical integrity by design.** Federation leadership positions are never overwritten — each change closes out the previous holder's record and opens a new one, preserving full history. The same principle applies to facility contracts and licenses.
- **Public vs. private file storage.** Uploaded files (champion photos, facility documents, message attachments) are split into publicly servable and access-controlled private storage at the filesystem level, not just by convention.
- **Designed for shared hosting.** No dependency on infrastructure unavailable on budget shared hosting — PostgreSQL instead of SQL Server, a static-file frontend build, no required background services.

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS recommended) and npm
- [PostgreSQL](https://www.postgresql.org/) (or Docker, see below)

### Backend setup

```bash
cd backend
dotnet restore
```

Create a local PostgreSQL database, or run one via Docker:

```bash
docker run --name sportfed-postgres -e POSTGRES_PASSWORD=yourpassword -e POSTGRES_DB=sportfedoms -p 5432:5432 -d postgres:16
```

Update the connection string in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=sportfedoms;Username=postgres;Password=yourpassword"
  }
}
```

Apply migrations and run:

```bash
dotnet ef database update
dotnet run
```

On first run, a seeded admin account is created automatically (see `appsettings.json` under `SeedAdmin` to set the initial username/password). The API's interactive Swagger docs are available at `/swagger` once running.

### Frontend setup

```bash
cd frontend
npm install
```

Create a `.env` file:

```
VITE_API_URL=http://localhost:5000/api
```

(adjust the port to match your backend's actual URL)

```bash
npm run dev
```

---

## Project Structure

```
sports-oms-dotnet-react/
├── backend/
│   ├── Controllers/       # API endpoints, one per resource
│   ├── Models/             # EF Core entities
│   ├── DTOs/                # Request/response shapes
│   ├── Services/           # Scoping, token generation, file storage
│   ├── Middleware/        # Global exception handling
│   └── Data/                # DbContext, seeding
└── frontend/
    ├── src/
    │   ├── api/              # HTTP calls per entity
    │   ├── hooks/           # React Query hooks
    │   ├── pages/
    │   │   ├── public/      # Unauthenticated statistics pages
    │   │   └── dashboard/  # Authenticated, role-scoped pages
    │   ├── layouts/
    │   └── components/
    └── public/
```

---

## Roles

| Role | Access |
|---|---|
| Admin | Full access; can create users and assign federation leadership positions |
| Manager | Full access except user creation and leadership assignment |
| M88 Officer | Manages MSY facilities, their contracts and licenses |
| M5 Officer | Manages private and government facilities and their licenses |
| Insurance Officer | Manages athlete registration statistics |
| Sport Officer | Manages champions and their medal records |
| Federation Representative | Messaging only, scoped to their assigned federation |

Every role above (except Admin/Manager, who operate nationally) also carries a geographic scope — National, Province, or City — which independently restricts which records they can see and modify.

---

## Roadmap

- [x] Core data model and authentication
- [x] Facilities, Champions, Federations, Athlete Statistics, Users, Messaging APIs
- [x] File uploads (public and private)
- [x] Public statistics endpoints
- [x] React dashboard and public frontend
- [ ] Historical data import tooling
- [ ] Production deployment

---

## Contributing

Issues and pull requests are welcome. Please open an issue to discuss significant changes before submitting a PR.

## License

This project is licensed under the [MIT License](LICENSE).

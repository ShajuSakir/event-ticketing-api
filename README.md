# Event Ticketing API

A RESTful API for managing events and ticket sales, built with ASP.NET Core 8 and EF Core (SQLite).

## Features

- **Event management** — create, retrieve, update, delete events with pricing tiers.
- **Ticket management** — purchase tickets, check availability, prevent overselling under concurrent purchases.
- **Reporting** — per-event and all-events sales summaries (tickets sold, revenue, remaining capacity, broken down by pricing tier).
- **JWT authentication** — login endpoint issues a signed token; `Admin` and `Customer` roles gate access to protected endpoints.

## Architecture

```
EventTicketing.sln
├── src/
│   ├── EventTicketing.Core            # Domain entities, DTOs, service interfaces, result types
│   ├── EventTicketing.Infrastructure  # EF Core DbContext, service implementations, SQLite
│   └── EventTicketing.Api             # Controllers, middleware, DI wiring, Swagger
└── tests/
    └── EventTicketing.Tests           # xUnit unit tests + WebApplicationFactory integration tests
```

### Preventing overselling

Each `PricingTier` uses an app-managed `Version` concurrency token. If concurrent purchases compete for the remaining tickets, EF Core detects the conflict, 
and `TicketService` reloads the latest inventory and retries the purchase. This prevents overselling without database-level table locking and is covered 
by a concurrency test with 20 simultaneous requests against a 10-seat tier.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

No external database server is required — the API uses a local SQLite file
(`eventticketing.db`, created automatically on first run in the working directory).

- JWT secret setup

The JWT signing secret is intentionally not stored in appsettings.json or source control.
For local development, configure it using ASP.NET Core User Secrets:

cd src/EventTicketing.Api
dotnet user-secrets set "Jwt:Secret" "your-long-random-secret"

The application reads the secret from the Jwt:Secret configuration key.
For production, the same configuration key can be supplied through an environment variable (Jwt__Secret) or a managed secrets store.

## Running the API

```bash
cd src/EventTicketing.Api
dotnet run
```

The database schema is created automatically at startup via `Database.EnsureCreated()`
(no manual migration step needed for local/dev use). Swagger UI opens automatically at:

```
http://localhost:5081/swagger
```

> **Switching to EF Core Migrations:** for production or CI pipelines with schema history
> requirements, replace `EnsureCreated()` in `Program.cs` with `Database.Migrate()`, and generate
> migrations with `dotnet ef migrations add InitialCreate --project src/EventTicketing.Infrastructure --startup-project src/EventTicketing.Api`.

## Running Tests

```bash
dotnet test
```

This runs both the unit tests (business logic against an in-memory SQLite DB) and the
integration tests (full HTTP pipeline via `WebApplicationFactory`).

## Authentication

The API uses JWT bearer tokens. 
For local development and demonstration purposes, Two demo accounts are seeded automatically on first run
(`SeedDemoUsersAsync` in `Program.cs`), since there is no registration endpoint:

**Note:** These demo credentials are for local/demo use only and should not be used in production.

| Username   | Password       | Role       |
|------------|----------------|------------|
| `admin`    | `Admin123!`    | `Admin`    |
| `customer` | `Customer123!` | `Customer` |

**Login:**

```json
POST /api/auth/login
{
  "username": "admin",
  "password": "Admin123!"
}
```

Returns a `LoginResponse` with a `token` and `expiresAtUtc`. Send the token on subsequent
requests as:

```
Authorization: Bearer <token>
```

**Using Swagger UI:** click the **Authorize** button (top right), paste the token as
`Bearer <token>` (or just the raw token, depending on Swagger version), then click
**Authorize**. All subsequent requests from the Swagger UI will include it automatically.

### Role requirements per endpoint

| Endpoint                                       | Access              |
|-------------------------------------------------|----------------------|
| `POST /api/auth/login`                          | Public               |
| `POST /api/events`                              | `Admin` only          |
| `GET /api/events?page=1&pageSize=20`            | Public               |
| `GET /api/events/{id}`                          | Public               |
| `PUT /api/events/{id}`                          | `Admin` only          |
| `DELETE /api/events/{id}`                       | `Admin` only          |
| `POST /api/events/{eventId}/tickets/purchase`   | Any authenticated user |
| `GET /api/events/{eventId}/tickets/availability`| Public               |
| `GET /api/reports/sales`                        | `Admin` only          |
| `GET /api/reports/sales/{eventId}`              | `Admin` only          |

## API Reference

### Events

| Method | Route                | Description               |
|--------|----------------------|----------------------------|
| POST   | `/api/events`         | Admin-only. Create an event with pricing tiers |
| GET    | `/api/events`         | List events with pagination |
| GET    | `/api/events/{id}`    | Get a single event        |
| PUT    | `/api/events/{id}`    | Admin-only. Update event details      |
| DELETE | `/api/events/{id}`    | Admin-only. Delete an event (blocked if tickets already sold) |

**List events with pagination:**

GET /api/events?page=1&pageSize=20
`page` defaults to `1` and `pageSize` defaults to `20`. `pageSize` must be between `1` and `100`.

**Create event example:**

```json
POST /api/events
{
  "name": "Tech Conference 2026",
  "description": "Annual developer conference",
  "venue": "Convention Center",
  "date": "2026-11-05",
  "time": "09:00:00",
  "totalCapacity": 100,
  "pricingTiers": [
    { "name": "General", "price": 50.00, "capacity": 80 },
    { "name": "VIP", "price": 150.00, "capacity": 20 }
  ]
}
```

### Tickets

| Method | Route                                          | Description             |
|--------|-------------------------------------------------|--------------------------|
| POST   | `/api/events/{eventId}/tickets/purchase`        | Requires authentication (any role). Purchase tickets from a pricing tier |
| GET    | `/api/events/{eventId}/tickets/availability`     | View remaining capacity per tier |

**Purchase example:**

```json
POST /api/events/{eventId}/tickets/purchase
{
  "pricingTierId": "...",
  "quantity": 2
}
```

Returns `409 Conflict` if the requested quantity exceeds remaining tier capacity.

### Reports

| Method | Route                          | Description                          |
|--------|--------------------------------|---------------------------------------|
| GET    | `/api/reports/sales`            | Admin-only. Sales summary for all events          |
| GET    | `/api/reports/sales/{eventId}`  | Admin-only. Sales summary for a single event      |

## Error Handling

- Validation errors (missing/invalid fields) → `400 Bad Request` with a `ProblemDetails` body.
- Missing resources → `404 Not Found`.
- Business rule violations (overselling, deleting an event with sales, etc.) → `409 Conflict`.
- Any unhandled exception → `500 Internal Server Error` with a generic `ProblemDetails` body
  (details are logged server-side via `ExceptionHandlingMiddleware`, not leaked to the client).

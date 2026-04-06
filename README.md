## SeatSync – Enterprise Event Ticketing Platform

### Overview

SeatSync is a high-consistency event ticketing platform designed to handle concurrent seat reservations for high-demand events.

The system focuses on:

* Preventing double-booking under heavy concurrency
* Maintaining strong transactional integrity
* Supporting temporary seat holds during checkout
* Generating secure, signed QR-code tickets for event validation

This project is built as a system design learning exercise with production-oriented architectural principles.

---

### Core Problem

In high-demand events, thousands of users may attempt to reserve the same seat simultaneously.

The system must guarantee:

* A seat can only be sold once
* A seat can only be held by one user at a time
* Hold expiration is enforced server-side
* All state transitions are atomic and transactional

---

### Architecture

* ASP.NET Core Web API (.NET 10)
* SQL Server (Dockerized)
* Entity Framework Core
* Swagger (OpenAPI)
* Clean multi-project architecture:

    * `SeatSync.Api`
    * `SeatSync.Domain`
    * `SeatSync.Infrastructure`

---

### Tech Stack

| Layer             | Technology             |
| ----------------- | ---------------------- |
| API               | ASP.NET Core (.NET 10) |
| ORM               | Entity Framework Core  |
| Database          | SQL Server 2022        |
| Containerization  | Docker                 |
| API Documentation | Swagger                |
| Development IDE   | JetBrains Rider        |

---

### Development Setup

1. Start full stack (SQL Server + API + Blazor Web):

```
docker compose up -d
```

2. Open:

```
http://localhost:5186/seating
```

3. Swagger UI:

```
http://localhost:5084/swagger
```

4. Stop:

```
docker compose down
```

5. Check service status/logs:

```
docker compose ps
docker compose logs -f api
docker compose logs -f web
```

6. Run Tests:

```
dotnet test
```

Notes:

* API startup now applies EF Core migrations automatically (with retry) so compose bootstraps the database schema.
* `api` and `web` run in .NET SDK containers via `dotnet run` (source is bind-mounted), so code changes are reflected without rebuilding images.
* If you add a new migration, generate it as usual:
  `dotnet ef migrations add {migration_name} -p SeatSync.Infrastructure -s SeatSync.Api`

---

### Design Principles

* Clean Architecture separation
* Domain-driven design mindset
* Strong consistency guarantees
* Explicit transactional boundaries
* Database-first invariants

---

### Blazor Seating Chart (UI)

`SeatSync.Web` includes an interactive seating chart page at `/seating`.

Seat states:

* `Available`: can be clicked to become `Selected`
* `Selected`: can be clicked to become `Available`
* `Held`: disabled/non-interactive
* `Booked`: disabled/non-interactive

Current limitation:

* Seat interactions are in-memory UI state only for now (no persistence or reservation API integration yet).

---

### UI Testing Criteria

1. Start the Blazor app and navigate to `/seating`.
2. Verify `Available`, `Selected`, `Held`, and `Booked` seats each have distinct visual styles.
3. Click an available seat and confirm it changes visually to selected.
4. Click the same selected seat and confirm it returns to available.
5. Verify held and booked seats are disabled and cannot be toggled.
6. Check the page at desktop and mobile widths to confirm the seat grid remains usable.

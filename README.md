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
http://localhost:5186/
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

7. Run SQL Server stored-procedure integration tests (optional, requires local SQL Server from compose):

```
SEATSYNC_RUN_SQLSERVER_TESTS=true dotnet test
```

Notes:

* API startup now applies EF Core migrations automatically (with retry) so compose bootstraps the database schema.
* `api` and `web` run in .NET SDK containers via `dotnet run` (source is bind-mounted), so code changes are reflected without rebuilding images.

Demo authentication accounts (seeded automatically):

* `admin@seatsync.demo` / `demo123` (`Admin`)
* `organizer@seatsync.demo` / `demo123` (`Organizer`)
* `attendee@seatsync.demo` / `demo123` (`Attendee`)
* `attendee2@seatsync.demo` / `demo123` (`Attendee`)
* `attendee3@seatsync.demo` / `demo123` (`Attendee`)

Manual seed command (runs migrations + seed data and exits):

```bash
./scripts/seed-db.sh
```

RBAC summary:

* `Admin`/`Organizer`: full event management (create/list/edit/delete), view event reservations, generate seat inventory, redeem tickets.
* Authenticated users: create holds, finalize orders, run mock payments, download/email receipts.

### Migrations and DB Updates

Create a migration (from repo root):

```bash
dotnet ef migrations add <MigrationName> -p SeatSync.Infrastructure -s SeatSync.Api
```

Apply migrations to the configured database:

```bash
dotnet ef database update -p SeatSync.Infrastructure -s SeatSync.Api
```

List applied/pending migrations:

```bash
dotnet ef migrations list -p SeatSync.Infrastructure -s SeatSync.Api
```

Compose workflow:

* `docker compose up -d` usually applies migrations automatically on API startup.
* If schema is stale, run manual update in the API container:

```bash
docker compose exec api dotnet ef database update -p SeatSync.Infrastructure -s SeatSync.Api
```

If you need a clean local reset (destructive for local DB data):

```bash
docker compose down -v
docker compose up -d
```

---

### Design Principles

* Clean Architecture separation
* Domain-driven design mindset
* Strong consistency guarantees
* Explicit transactional boundaries
* Database-first invariants

---

### Blazor Seating Chart (UI)

`SeatSync.Web` includes:

* `/events` to choose an event
* `/seating/{eventId}` to select seats and create a hold
* `/checkout/{holdId}` to complete payment
* `/receipt/{orderId}` to open/download the PDF receipt
* `/admin/events` manager dashboard for event CRUD
* `/admin/events/new` create event
* `/admin/events/{eventId}/edit` edit event details
* `/admin/events/{eventId}/reservations` view reservations

Seat states:

* `Available`: can be clicked to become `Selected`
* `Selected`: can be clicked to become `Available`
* `Held`: disabled/non-interactive
* `Booked`: disabled/non-interactive

Backend integration:

* Seat map is fetched from `GET /api/events/{eventId}/seats`.
* Proceed-to-payment reserves seats with `POST /api/holds`.
* Checkout runs `POST /api/orders/finalize` and `POST /api/orders/{orderId}/payments/mock`.
* Receipts are available via `GET /api/orders/{orderId}/receipt` and `GET /api/orders/{orderId}/receipt/pdf`.

Event seat inventory generation:

* Create event with seat-copy in one call: `POST /api/events` with `copySeatsFromEventId`.
* Or generate seat inventory idempotently later: `POST /api/events/{eventId}/seat-inventory/generate`.
* Update event details: `PUT /api/events/{eventId}`.
* Delete event: `DELETE /api/events/{eventId}`.
* View event reservations: `GET /api/events/{eventId}/reservations`.

---

### Stored Procedure Workflow

Seat reservation state transitions are handled by SQL Server stored procedures:

* `sp_CreateSeatHold`
* `sp_ReleaseExpiredHolds`
* `sp_FinalizeSeatOrder`

Concurrency strategy:

* Explicit transactions (`BEGIN TRAN` / `COMMIT` / rollback-on-error via `XACT_ABORT`)
* `SERIALIZABLE` isolation for hold/finalize critical sections
* `UPDLOCK` + `HOLDLOCK` on targeted seat/hold/order rows
* Unique idempotency key index on `(UserId, IdempotencyKey)` for safe finalize retries

Hot-path indexes:

* `IX_SeatStatuses_EventId_SeatId` (unique)
* `IX_SeatStatuses_HoldId_State`
* `IX_SeatStatuses_OrderId_State`
* `IX_Holds_State_ExpiresAt`
* `IX_Orders_UserId_IdempotencyKey` (unique)

Known limitations:

* Hold expiration is currently triggered on reservation/finalization calls (and optional manual endpoint `POST /api/holds/release-expired`), not by a dedicated background worker.

---

### UI Testing Criteria

1. Start the Blazor app and navigate to `/seating`.
2. Verify `Available`, `Selected`, `Held`, and `Booked` seats each have distinct visual styles.
3. Click an available seat and confirm it changes visually to selected.
4. Click the same selected seat and confirm it returns to available.
5. Verify held and booked seats are disabled and cannot be toggled.
6. Check the page at desktop and mobile widths to confirm the seat grid remains usable.

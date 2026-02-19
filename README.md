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

1. Start SQL Server:

```
docker compose up -d
```

2. Apply migrations:

```
dotnet ef database update -p SeatSync.Infrastructure -s SeatSync.Api
```

3. Run API:

```
dotnet run --project SeatSync.Api
```

Swagger UI available at:

```
http://localhost:5084/swagger
```

---

### Design Principles

* Clean Architecture separation
* Domain-driven design mindset
* Strong consistency guarantees
* Explicit transactional boundaries
* Database-first invariants

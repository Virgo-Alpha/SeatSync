# SeatSync Architecture

## Overview

SeatSync is a high-consistency event ticketing system designed to prevent seat double-booking under concurrent load.

The architecture prioritizes:

- Strong data consistency
- Explicit transactional boundaries
- Separation of concerns
- Clear domain modeling

---

## System Components

### 1. API Layer (SeatSync.Api)

- ASP.NET Core Web API (.NET 10)
- Exposes REST endpoints
- Handles request validation
- Orchestrates domain logic
- Returns DTOs (never domain entities)

### 2. Domain Layer (SeatSync.Domain)

- Contains core business models
- Encapsulates invariants
- Defines state transitions
- Independent of infrastructure

Examples:
- Event
- Seat
- Hold
- Order
- Ticket

### 3. Infrastructure Layer (SeatSync.Infrastructure)

- Entity Framework Core
- SQL Server persistence
- Migrations
- Future: Redis integration
- Future: external payment providers

---

## High-Level Data Flow

1. Client requests seat map.
2. User selects seats.
3. API creates a temporary Hold (transactional).
4. User completes payment.
5. Hold is converted to Order (atomic).
6. Tickets are issued with signed JWT.
7. Event staff validate ticket via QR scan.

---

## Deployment Model (Development)

- SQL Server runs in Docker.
- API runs locally via `dotnet run`.
- EF migrations applied manually during development.

---

## Future Enhancements

- Redis for seat lock optimization
- SignalR for real-time seat updates
- Background job for hold expiration
- Payment provider abstraction

# ADR-0002: SQL Server as Source of Truth

## Status
Accepted

## Context

Seat reservation requires strict consistency guarantees.

Redis alone cannot guarantee durability or transactional integrity.

## Decision

All seat state transitions are enforced via SQL Server transactions.

Redis (future) may act only as performance optimization.

## Consequences

Pros:
- Strong consistency
- ACID guarantees
- Deterministic state

Cons:
- Slightly higher write latency compared to eventual systems

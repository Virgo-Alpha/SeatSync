# ADR-0001: Adopt Clean Architecture Separation

## Status
Accepted

## Context

SeatSync aims to be maintainable, testable, and scalable.

A layered separation prevents:

- Infrastructure leakage into domain
- Tight coupling
- Hard-to-test business logic

## Decision

Adopt three primary layers:

- Api
- Domain
- Infrastructure

Dependencies:

Api → Infrastructure → Domain  
Domain has no outward dependencies.

## Consequences

Pros:
- Clear separation of concerns
- Easier testing
- Better long-term maintainability

Cons:
- Slight initial complexity

# ADR-0003: Dockerized SQL Server for Development

## Status
Accepted

## Context

Development environments must be reproducible.

Local SQL installations vary by developer machine.

## Decision

Use Docker container for SQL Server during development.

## Consequences

Pros:
- Consistent environment
- Easy onboarding
- No OS-specific setup

Cons:
- Requires Docker

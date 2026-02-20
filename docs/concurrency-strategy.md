# Concurrency Strategy

## Core Problem

High-demand events may cause thousands of users to attempt reserving the same seat simultaneously.

SeatSync must guarantee:

- A seat can only be sold once.
- A seat can only be held by one user at a time.
- Holds expire deterministically.
- Final sale is atomic.

---

## Source of Truth

SQL Server is the single source of truth.

Redis (future) may optimize performance, but correctness is always enforced at the database layer.

---

## Concurrency Control Mechanisms

### 1. Transaction Isolation

Seat reservation and finalization operations will use:

- Explicit SQL transactions
- `UPDLOCK` and `ROWLOCK`
- Or SERIALIZABLE isolation (if necessary)

Goal:
Prevent lost updates and race conditions.

---

### 2. Row-Level State Tracking

Each seat maintains:

- Current state (Available, Held, Sold)
- Associated HoldId (if held)
- Associated OrderId (if sold)
- RowVersion (optimistic concurrency token)

---

### 3. Idempotency

Order finalization will require:

- Client-provided Idempotency Key
- Unique constraint at database level

This prevents duplicate charges and duplicate ticket issuance.

---

### 4. Expiration Enforcement

Hold expiration is enforced server-side via:

- Timestamp comparison
- Background cleanup job (future)
- Validation during finalization

Clients cannot extend holds without server approval.

---

## Failure Scenarios Covered

- Concurrent seat selection
- Payment retries
- API retries
- Network timeouts
- Process crashes during transaction

---

## Non-Goals

- Eventual consistency
- Optimistic-only locking for final sale

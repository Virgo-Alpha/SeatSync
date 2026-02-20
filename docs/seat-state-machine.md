# Seat State Machine

## Seat States

Each seat exists in exactly one of the following states:

1. Available
2. Held
3. Sold

---

## State Transitions

Available → Held
- Trigger: CreateHold
- Condition: Seat not currently held or sold
- Transactional update

Held → Sold
- Trigger: FinalizeOrder
- Condition:
    - Hold exists
    - Hold not expired
    - Hold belongs to requesting user
- Atomic conversion

Held → Available
- Trigger:
    - Hold expiration
    - Hold cancellation
- Cleanup operation

Available → Sold
- Not allowed directly

Sold → Any
- Not allowed

---

## Invariants

1. A seat can be sold only once.
2. A seat cannot be held by two users simultaneously.
3. Only the owning hold can finalize the seat.
4. Expired holds are invalid.

---

## Database Enforcement Strategy

- Single row per seat per event
- Status column
- HoldId column
- OrderId column
- RowVersion column
- Unique constraint on Sold seats

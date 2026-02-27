# ConcurrentBooking

A production-grade slot-booking API that demonstrates **real concurrency guarantees** under high contention: anti-overbooking, idempotency, and automatic hold expiration ; all backed by PostgreSQL, not in-memory tricks.

---

## Guarantees

| Guarantee | Mechanism |
|---|---|
| **Anti-overbooking** | `UNIQUE` constraint on `bookings.SlotId` ; the DB is the final arbiter, not application-level locks |
| **Single active hold per slot** | Partial unique index `idx_holds_slot_active` on `holds(SlotId) WHERE Status = 0` (Postgres) |
| **Idempotency** | HTTP `Idempotency-Key` header handled by `IdempotencyMiddleware`; stores full response, replays on retry |
| **Hold expiration** | `HoldExpirationService` (BackgroundService) scans every 30 s and transitions stale Active → Expired holds |
| **DB-level request deduplication** | `RequestId UNIQUE` on both `holds` and `bookings` as a secondary safety net |

---

## Flow: Hold → Confirm

```
Client                      API                           PostgreSQL
  │                           │                               │
  │─POST /slots/{id}/hold────▶│                               │
  │  Idempotency-Key: <key>   │─INSERT holds (Active)────────▶│
  │                           │  ← constraint: 1 active/slot  │
  │◀─201 { holdId, expiresAt }│                               │
  │                           │                               │
  │  (within TTL = 30 s)      │                               │
  │                           │                               │
  │─POST /holds/{id}/confirm─▶│                               │
  │  Idempotency-Key: <key>   │─INSERT bookings──────────────▶│
  │                           │  ← UNIQUE(SlotId) blocks race │
  │◀─201 { bookingId }────────│  UPDATE holds SET Status=1    │
  │                           │                               │
  │  (retry same key)         │                               │
  │─POST /holds/{id}/confirm─▶│                               │
  │  Idempotency-Key: <key>   │─Middleware replay (no DB hit)─│
  │◀─201 { bookingId } ───────│                               │
```

If two clients race the confirm step, the second `INSERT` violates the `UNIQUE(bookings.SlotId)` constraint and the API returns **409 Conflict**.

---

## Running locally

**Prerequisites:** Docker + Docker Compose

```bash
# Start Postgres + API
docker-compose up --build

# API will be available at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Seed a slot, then create a hold

```bash
# 1. Insert a resource + slot directly via psql (or add a seed endpoint)
docker exec -it <postgres-container> psql -U postgres -d concurrent_booking

# 2. Hold a slot
curl -X POST http://localhost:5000/api/slots/{slotId}/hold \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: my-unique-key-1" \
  -d '{"customerId":"<guid>"}'

# 3. Confirm the booking
curl -X POST http://localhost:5000/api/holds/{holdId}/confirm \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: my-unique-key-2" \
  -d '{"customerId":"<guid>"}'
```

---

## Running the tests

### Unit tests (in-memory, no Docker needed)

```bash
dotnet test --filter "FullyQualifiedName~ConcurrencyTests"
```

### Integration / stress tests (requires Docker)

The integration tests use **Testcontainers** to spin up a real PostgreSQL instance automatically ; no manual setup needed.

```bash
dotnet test --filter "FullyQualifiedName~ConcurrencyIntegrationTests"
```

**What the stress tests prove:**

- `OnlyOneHoldSucceedsWhen100ConcurrentHoldRequestsRace` ; 100 tasks race to hold the same slot; exactly 1 wins via the Postgres partial unique index.
- `OnlyOneBookingSucceedsWhen100ConcurrentConfirmRequestsRace` ; 100 tasks race to confirm the same hold; exactly 1 booking is created via the `UNIQUE(bookings.SlotId)` constraint.
- `ConfirmAfterHoldExpiredReturnsError` ; confirms that a 1 ms TTL hold correctly blocks confirmation.

### Coverage

```bash
dotnet tool install -g dotnet-coverage   # one-time
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

---

## Architecture decisions

### Why DB constraints instead of in-memory locks?

| Approach | In-memory lock / `ConcurrentDictionary` | **DB constraint (chosen)** |
|---|---|---|
| Multi-instance safe | ❌ No ; each pod has its own memory | ✅ Yes ; single source of truth |
| Survives restart | ❌ No | ✅ Yes |
| Throughput | High (no I/O) | High (index seek, no row scan) |
| Correctness proof | Hard to reason about | Declarative ; the DB spec defines it |

The `UNIQUE` constraint on `bookings.SlotId` and the partial index `idx_holds_slot_active` are the **only** places that need to be correct. Everything else is defense-in-depth.

### Why a Middleware for idempotency instead of handler logic?

Idempotency is a **cross-cutting concern**, not business logic. Putting it in the middleware:

- Keeps handlers focused on domain rules.
- Applies uniformly to every mutating endpoint without duplication.
- Enables richer replay (full HTTP status + body) without the handler knowing about it.

### Hold TTL and expiration

Holds expire after **30 seconds** by default. `HoldExpirationService` runs every 30 seconds and bulk-updates stale `Active` holds to `Expired`. This releases the partial unique index, allowing a new hold on the same slot. The service creates its own DI scope per tick to avoid long-lived `DbContext` instances.

---

## Project structure

```
ConcurrentBooking.Domain/         Domain entities and invariants
ConcurrentBooking.Application/    Use-case handlers, repository interfaces, DTOs
ConcurrentBooking.Infrastructure/ EF Core repositories, migrations, BookingDbContext
Api/                              ASP.NET Core controllers, IdempotencyMiddleware, HoldExpirationService
ConcurrentBooking.Tests/          Unit tests (in-memory) + Integration stress tests (Testcontainers)
```

---

## Roadmap

| Priority | Item |
|---|---|
| P1 | Outbox pattern for `BookingConfirmed` / `HoldExpired` domain events |
| P1 | OpenTelemetry tracing + structured logging with correlation-id |
| P1 | Rate limiting per `customerId` to prevent hold-spam |
| P2 | Redis distributed lock for ultra-hot slots (single-writer pattern) |
| P2 | CQRS read model ; optimized availability query without touching the write tables |

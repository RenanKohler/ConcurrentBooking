# ConcurrentBooking

ConcurrentBooking is an ASP.NET Core API plus a WPF client that demonstrates two things at once:

- clinical slot discovery for appointment booking
- real concurrency guarantees for `hold -> confirm` booking under contention

The current baseline is a clinical scheduling demo with seeded specialties, units, professionals, and future slots. Availability is read through HTTP APIs, while the write path is still the contention-safe booking core backed by PostgreSQL constraints.

## Current capabilities

- Search specialties with `GET /api/specialties`
- Search clinic units with `GET /api/units`
- Search professionals by specialty, date, and optional unit with `GET /api/professionals`
- Load available slots by professional, date, period, and unit with `GET /api/professionals/{professionalId}/availability`
- Create a temporary hold with `POST /api/slots/{slotId}/hold`
- Confirm a booking with `POST /api/holds/{holdId}/confirm`
- Expire stale holds in the background
- Replay idempotent HTTP responses when the `Idempotency-Key` header is reused

## Guarantees

| Guarantee | Mechanism |
|---|---|
| Anti-overbooking | `UNIQUE` constraint on `bookings.SlotId` |
| Single active hold per slot | partial unique index `idx_holds_slot_active` on `holds(SlotId)` where `Status = 0` |
| Hold expiration | `HoldExpirationService` transitions expired active holds to `Expired` |
| Request deduplication | `RequestId` uniqueness on `holds` and `bookings` |
| HTTP idempotency replay | `IdempotencyMiddleware` stores and replays responses by route + `Idempotency-Key` header |

The database is the final arbiter for booking conflicts. Application checks are only a fast pre-check.

## Clinical model

The generic `Resource` model is no longer the source of truth. The current schema uses:

- `Specialty`
- `ClinicUnit`
- `Professional`
- `Slot` linked to `ProfessionalId` and `ClinicUnitId`
- `Hold`
- `Booking`
- `IdempotencyRecord`

Demo data is seeded automatically on startup for local development.

## API surface

### Read endpoints

- `GET /api/specialties`
- `GET /api/units`
- `GET /api/professionals?specialtyId={guid}&date={yyyy-MM-dd}&unitId={guid?}`
- `GET /api/professionals/{professionalId}/availability?date={yyyy-MM-dd}&period=Morning|Afternoon|Evening&unitId={guid}`

Availability periods:

- `Morning`: 06:00-11:59
- `Afternoon`: 12:00-17:59
- `Evening`: 18:00-22:59

### Write endpoints

- `POST /api/slots/{slotId}/hold`
- `POST /api/holds/{holdId}/confirm`

Current request contracts are documented exactly as implemented today:

`POST /api/slots/{slotId}/hold`

```json
{
  "customerId": "00000000-0000-0000-0000-000000000000",
  "idempotencyKey": "hold-request-key"
}
```

Success response:

```json
{
  "holdId": "00000000-0000-0000-0000-000000000000",
  "expiresAt": "2026-03-10T12:00:00Z"
}
```

`POST /api/holds/{holdId}/confirm`

```json
{
  "customerId": "00000000-0000-0000-0000-000000000000",
  "idempotencyKey": "confirm-request-key"
}
```

Success response:

```json
{
  "bookingId": "00000000-0000-0000-0000-000000000000"
}
```

Notes about idempotency:

- The request body currently carries `idempotencyKey` for the application/use-case layer.
- The optional `Idempotency-Key` HTTP header is what activates middleware-level response replay.
- To reproduce the current full behavior, send both.

## Running locally

Prerequisites:

- Docker
- Docker Compose
- .NET SDK 8 or newer

The current demo migration path is development-oriented. If you already ran an older version of the project, recreate the local database volume before starting again.

```bash
docker compose down -v
docker compose up --build
```

After startup:

- API: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`

## Demo flow

The API seeds specialties, units, professionals, and future slots automatically. A typical clinical flow is:

1. Load specialties.
2. Load units.
3. Search professionals by specialty and date.
4. Load availability for a selected professional, unit, date, and period.
5. Create a hold for one available slot.
6. Confirm the hold.

Example:

```bash
curl http://localhost:5000/api/specialties
curl http://localhost:5000/api/units
curl "http://localhost:5000/api/professionals?specialtyId=<specialtyId>&date=2026-03-10"
curl "http://localhost:5000/api/professionals/<professionalId>/availability?date=2026-03-10&period=Morning&unitId=<unitId>"

curl -X POST http://localhost:5000/api/slots/<slotId>/hold \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: hold-http-key" \
  -d '{"customerId":"<customer-guid>","idempotencyKey":"hold-request-key"}'

curl -X POST http://localhost:5000/api/holds/<holdId>/confirm \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: confirm-http-key" \
  -d '{"customerId":"<customer-guid>","idempotencyKey":"confirm-request-key"}'
```

## WPF client

The WPF client now mirrors the implemented clinical flow instead of manual slot CRUD.

```bash
dotnet run --project ConcurrentBooking.WpfClient
```

The client lets you:

- choose specialty
- optionally narrow by unit
- pick date and period
- load matching professionals
- load available slots for the selected professional
- create hold and confirm booking using the selected slot

Default API base URL in the client is `http://localhost:5000/`.

## Tests

Build:

```bash
dotnet build
```

Non-Docker baseline:

```bash
dotnet test --filter "FullyQualifiedName!~ConcurrentBooking.Tests.Integration.ConcurrencyIntegrationTests"
```

This covers:

- unit tests for availability filtering
- non-Docker concurrency tests with in-memory repositories
- HTTP integration tests with `WebApplicationFactory`

Docker-backed PostgreSQL concurrency tests:

```bash
dotnet test --filter "FullyQualifiedName~ConcurrentBooking.Tests.Integration.ConcurrencyIntegrationTests"
```

These verify:

- only one hold wins under 100 concurrent hold attempts
- only one booking wins under 100 concurrent confirm attempts
- expired holds cannot be confirmed

## Project structure

```text
ConcurrentBooking.Domain/         Domain entities and invariants
ConcurrentBooking.Application/    Use cases, DTOs, discovery contracts
ConcurrentBooking.Infrastructure/ EF Core, repositories, migrations, discovery service
Api/                              ASP.NET Core host, controllers, middleware, seed/init services
ConcurrentBooking.WpfClient/      Desktop client for discovery + hold/confirm flow
ConcurrentBooking.Tests/          Unit, HTTP integration, and PostgreSQL concurrency tests
docs/                             Functional requirements and supporting docs
```

## References

- Functional requirements: `docs/documento-requisitos-agendamento.md`
- Local infrastructure: `docker-compose.yml`

## Next backlog

- scheduling policy rules such as min/max booking window and patient conflict validation
- cancel and reschedule flows
- authentication, authorization, and receptionist audit trail
- observability and rate limiting

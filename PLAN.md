# Busca e Disponibilidade Clínica

## Summary
Implement the next slice as a clinical discovery flow on top of the existing booking engine: specialty selection, optional unit filter, professional listing, and available-slot listing for a selected date and period. Keep the current `hold -> confirm` write flow intact, keep controller-based ASP.NET Core, and cover backend plus WPF updates.

## Key Changes
- Replace the generic `Resource` concept with explicit clinical master data:
  - `Specialty`
  - `ClinicUnit`
  - `Professional` with a required `SpecialtyId`
  - `Slot` updated to reference `ProfessionalId` and `ClinicUnitId`
- Keep `Hold`, `Booking`, and idempotency behavior unchanged; availability remains derived from slot state plus active holds/bookings.
- Add a new migration that introduces the clinical tables/columns and seeds demo data only:
  - specialties, units, professionals
  - future slots for seeded professionals
- Treat this as a dev/demo schema evolution, not a data-preserving migration. Recreating the local database is acceptable.
- Move startup seeding out of inline `Program.cs` code into a dedicated seeder/service invoked at startup so `Program.cs` stays focused on host wiring.

## Public APIs / Contracts
- Keep existing write endpoints unchanged:
  - `POST /api/slots/{slotId}/hold`
  - `POST /api/holds/{holdId}/confirm`
- Add read endpoints:
  - `GET /api/specialties`
  - `GET /api/units`
  - `GET /api/professionals?specialtyId={guid}&date={yyyy-MM-dd}&unitId={guid?}`
  - `GET /api/professionals/{professionalId}/availability?date={yyyy-MM-dd}&period=Morning|Afternoon|Evening&unitId={guid}`
- Return DTOs only; do not expose EF entities.
- `GET /api/professionals` should return flattened professional+unit rows so optional multi-unit search stays unambiguous.
- `GET /availability` should return only available slots, excluding:
  - slots with an active booking
  - slots with an active, non-expired hold
- Add an `AvailabilityPeriod` enum to the API contract.
- Period defaults:
  - `Morning`: 06:00-11:59
  - `Afternoon`: 12:00-17:59
  - `Evening`: 18:00-22:59
- Use normal `[ApiController]` validation for missing/invalid query parameters; do not retrofit the existing write endpoints to a new error contract in this slice.

## WPF Changes
- Replace the manual slot-creation flow with a clinical discovery UI:
  - specialty combo
  - optional unit combo
  - date picker
  - period combo
  - professionals grid
  - available slots grid
- Selecting a professional row loads its availability.
- Selecting a slot writes the chosen `SlotId` into the existing hold/confirm panel.
- Keep API base URL and the current hold/confirm actions so the new read flow feeds the existing reservation flow.
- Do not add WPF-side admin CRUD for specialties/units/professionals/slots.

## Test Plan
- Add unit tests for availability filtering:
  - period mapping
  - booked slot excluded
  - active hold excluded
  - expired hold becomes visible again
- Add HTTP integration tests with `WebApplicationFactory<Program>` for the new read endpoints and DTO shapes.
- Keep the current PostgreSQL/Testcontainers concurrency tests for `hold` and `confirm`.
- Add one database-backed integration test proving the availability query hides a slot after hold creation and after booking confirmation.

## Assumptions
- No real authentication/authorization in this iteration.
- No receptionist workflow or audit trail yet.
- Unit filter is optional in search, but `unitId` is required when fetching a selected professional’s availability.
- Seed data only; no administrative endpoints for master data.
- Target framework stays on `.NET 8` / ASP.NET Core 8 for this slice.

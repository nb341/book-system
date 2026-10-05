# PRD — Booking System (45-minute build)

> Source: the requirements pasted in the planning request. The assessment PDF was not read (outside the working directory). Verify against it before submitting.

## 1. Goal
Authenticated booking platform. **Providers** define resources and available time slots. **Customers** register, book a slot with payment, and can cancel or reschedule. No double booking, ever.

## 2. Scale assumptions (design for it, don't build it)
10M users, ~1M bookings/day (~12/s average, ~100/s peak). One SQL database with proper indexes handles this. Overbooking safety must come from a **DB constraint**, not app-level checks. The scale story (partitioning, read replicas, queue for payments, cache for slot search) goes in the README only.

## 3. Functional requirements
| # | Requirement |
|---|---|
| F1 | Register (role: Customer or Provider) and log in. All endpoints except register/login require a JWT. |
| F2 | Provider CRUD on own resources (name, description). |
| F3 | Provider defines time slots for a resource (start, end, price). Slots must not overlap within a resource. Slots with a confirmed or pending booking can't be deleted. |
| F4 | Customer browses resources and their available (future, unbooked) slots. |
| F5 | Customer books a slot. Payment is taken as part of booking. |
| F6 | Payment fails → booking is NOT confirmed and the slot is released. |
| F7 | A slot has at most one active (Pending/Confirmed) booking. Concurrent attempts: exactly one wins, the others get 409. |
| F8 | Customer cancels own booking (frees the slot; refund is mocked). |
| F9 | Customer reschedules own booking to another available slot, atomically. If the new slot is taken or payment differs and fails, the original booking stays untouched. |
| F10 | Customer sees "my bookings"; provider sees bookings on their resources. |

## 4. Business rules and edge cases
- Active booking = status `Pending` or `Confirmed`. Enforced by a unique filtered index on `Bookings(SlotId)`.
- Flow: insert `Pending` (claims slot) → charge → `Confirmed`, or `PaymentFailed` (releases slot). Never charge without holding the slot first.
- Idempotency: client sends an `Idempotency-Key`; repeating it returns the original booking instead of charging twice.
- Can't book past slots, own-provider slots, or an already-taken slot.
- Cancel: only owner, only `Confirmed`, only before slot start. Cancelling twice is a no-op/409.
- Reschedule: same resource only (MVP), new slot must be free and in the future. Implemented as one transaction: claim new slot, cancel old. Price difference is ignored in MVP (document it).
- Stale `Pending` rows (crash mid-payment): MVP ignores; README notes a sweeper job.
- Authorization: customers can't touch provider endpoints and vice versa; users only see their own data (403/404, not leakage).
- Times are stored and compared in UTC.

## 5. Architecture
Single ASP.NET Core (net10.0) Web API + React SPA + one database. No microservices, queues, caches, or CQRS.
- **API:** controllers → thin services → EF Core `DbContext`. `IPaymentGateway` is the only abstraction (mock: succeeds unless card token is `fail`, to demo failure).
- **DB:** PostgreSQL 16 via `docker-compose.yml` (service `db`, port 5432, named volume, healthcheck). EF Core with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x. Connection string in `appsettings.Development.json`. `timestamptz` for all times.
- **Auth:** PBKDF2 via `PasswordHasher<User>`, JWT bearer, role claim.
- **UI:** Vite + React 19 + TypeScript, react-router, plain `fetch` wrapper, token in memory + localStorage.

## 6. Data model
- `Users(Id, Email UNIQUE, PasswordHash, Name, Role, CreatedAt)`
- `Resources(Id, ProviderId→Users, Name, Description, CreatedAt)` — index `ProviderId`
- `Slots(Id, ResourceId→Resources, StartUtc, EndUtc, PriceCents, CHECK End>Start)` — index `(ResourceId, StartUtc)`
- `Bookings(Id, SlotId→Slots, CustomerId→Users, Status, AmountCents, PaymentRef, IdempotencyKey, CreatedAt, UpdatedAt)`
  - **UNIQUE** `(SlotId)` WHERE `Status IN ('Pending','Confirmed')`  ← no-overbooking guarantee
  - UNIQUE `(CustomerId, IdempotencyKey)`; index `(CustomerId, CreatedAt)`
- `Payments` table is omitted in MVP; `PaymentRef` on the booking is enough.

## 7. API contract
All JSON, `Authorization: Bearer <jwt>` except auth. Errors use ProblemDetails (`400` validation, `401`, `403`, `404`, `409` conflict, `402` payment failed).

| Method & path | Role | Body → Response |
|---|---|---|
| POST `/api/auth/register` | public | `{email,password,name,role}` → `201 {token,user}` |
| POST `/api/auth/login` | public | `{email,password}` → `{token,user}` |
| GET `/api/resources` | any | → `[{id,name,description,providerName}]` |
| GET `/api/resources/{id}/slots?from&to` | any | → available slots `[{id,startUtc,endUtc,priceCents}]` |
| POST `/api/provider/resources` | Provider | `{name,description}` → `201 resource` |
| GET `/api/provider/resources` | Provider | → own resources |
| POST `/api/provider/resources/{id}/slots` | Provider | `{startUtc,endUtc,priceCents}` → `201 slot` (409 if overlap) |
| DELETE `/api/provider/slots/{id}` | Provider | `204` (409 if booked) |
| GET `/api/provider/bookings` | Provider | → bookings on own resources |
| POST `/api/bookings` | Customer | header `Idempotency-Key`; `{slotId,cardToken}` → `201 booking` / `409` taken / `402` payment failed |
| GET `/api/bookings` | Customer | → own bookings |
| POST `/api/bookings/{id}/cancel` | Customer | → `200 booking` |
| POST `/api/bookings/{id}/reschedule` | Customer | `{newSlotId}` → `200 booking` / `409` |

`booking = {id,slotId,resourceName,startUtc,endUtc,status,amountCents}`

## 8. Frontend (MVP screens)
Login/Register · Customer: resource list → slot picker + pay (card token input) → My Bookings (cancel, reschedule via slot picker) · Provider: My Resources, add resource, manage slots, bookings list. Role-based route guard. Show 409/402 errors clearly.

## 9. Explicitly NOT building
Microservices, message queues, Redis, real payment provider/Stripe, email/SMS, refresh tokens, OAuth, social login, admin role, recurring availability rules, time zones UI, search/filters beyond date, pagination UI (API supports `take/skip` only if time), multi-resource bookings, partial refunds, price-difference settlement on reschedule, CQRS/MediatR/repository layer, Docker/K8s, CI, i18n.

## 10. Task breakdown and dependencies
| Id | Task | Owner agent | Depends on | Est. |
|---|---|---|---|---|
| T0 | Lock this PRD, contract and schema | architect | — | 3m |
| T1a | `docker-compose.yml` (Postgres 16), connection string, Npgsql package | db-designer | T0 | 3m |
| T1 | EF entities, DbContext, partial unique index, migration | db-designer | T1a | 7m |
| T2 | Auth (register/login/JWT/roles) + CORS | backend | T1 | 6m |
| T3 | Provider resource/slot endpoints + overlap check | backend | T1, T2 | 6m |
| T4 | Booking/cancel/reschedule + mock payment + concurrency test | backend | T1, T2 | 12m |
| T5 | UI scaffold: router, api client, auth context, guards | frontend | T0 (contract only) | 6m |
| T6 | Customer screens | frontend | T5 (mock → real after T4) | 8m |
| T7 | Provider screens | frontend | T5 (real after T3) | 6m |
| T8 | Integration pass, README, demo script | architect | all | 5m |

Parallel lanes after T0: **DB→Backend** and **Frontend** run concurrently against the contract in §7. Backend T3/T4 run in parallel after T2.

## 11. Risks
1. Concurrency bug in booking → mitigated by DB index plus a test firing parallel requests at one slot.
2. Payment/booking inconsistency → Pending-first flow; failure path releases slot; test it.
3. Reschedule partial failure → single transaction; test taken-slot case.
4. Time budget → cut order: provider slot deletion, provider bookings view, reschedule UI polish.
5. Postgres specifics: partial unique index via EF `HasFilter("\"Status\" IN ('Pending','Confirmed')")` (quoted identifiers); Npgsql requires UTC `DateTime`/`DateTimeOffset` for `timestamptz`; unique violation is SqlState `23505` (map to 409). Docker must be running before the API starts.
6. Scaffold is net10 / Vite 8 / React 19 compiler; `dotnet-ef` global tool is 8.0.11 — use EF Core 10 packages and update the tool (`dotnet tool update -g dotnet-ef`), or apply schema with `Database.Migrate()`/`EnsureCreated` as fallback.
7. Assessment PDF not reviewed — requirements may include details not captured here.

## 12. Definition of done
Register as both roles, provider creates resource + slots, customer books (success and forced payment failure), second customer gets 409 on the same slot, cancel frees slot, reschedule works atomically, parallel-booking test passes, README explains scale-up path.

# API Contract

Derived from `PRD.md` §7 (PRD wins on conflict). JSON everywhere. All instants are ISO-8601 UTC strings (`2026-11-01T10:00:00Z`). Money is integer cents.

## Conventions

- **Auth:** `Authorization: Bearer <jwt>` on everything except register/login. Missing/invalid token gives 401. Wrong role gives 403.
- **Ownership:** ids the caller does not own return 404.
- **Error shape (RFC 7807 ProblemDetails):**
```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Slot already booked",
  "status": 409,
  "detail": "The selected slot is no longer available.",
  "traceId": "00-abc...",
  "errors": { "email": ["Email is required."] }
}
```
`errors` appears only on 400 validation. The UI displays `detail` (fall back to `title`).
- **Idempotency-Key (POST /api/bookings only):** required header, any string up to 64 chars (UUID recommended), unique per customer. Missing gives 400. Same key replays the original outcome with no second charge: 201 with the same booking, or the same 402 if it ended `PaymentFailed`. After a decline the client must use a **new** key to retry. A concurrent duplicate resolves to the original result.
- **Booking status values:** `Pending`, `Confirmed`, `PaymentFailed`, `Cancelled` (strings). Only `Confirmed` bookings are normally returned from write endpoints; lists may include all.

## Endpoints

| Method & path | Auth / role | Success | Errors |
|---|---|---|---|
| POST `/api/auth/register` | public | 201 | 400, 409 (email exists) |
| POST `/api/auth/login` | public | 200 | 400, 401 |
| GET `/api/resources` | any authenticated | 200 | 401 |
| GET `/api/resources/{id}/slots?from&to` | any authenticated | 200 | 400, 401, 404 |
| POST `/api/provider/resources` | Provider | 201 | 400, 401, 403 |
| GET `/api/provider/resources` | Provider | 200 | 401, 403 |
| POST `/api/provider/resources/{id}/slots` | Provider (owner) | 201 | 400, 401, 403, 404, 409 (overlap) |
| DELETE `/api/provider/slots/{id}` | Provider (owner) | 204 | 401, 403, 404, 409 (has bookings) |
| GET `/api/provider/bookings` | Provider | 200 | 401, 403 |
| POST `/api/bookings` | Customer | 201 | 400, 401, 402, 403, 404, 409 |
| GET `/api/bookings` | Customer | 200 | 401, 403 |
| POST `/api/bookings/{id}/cancel` | Customer (owner) | 200 | 401, 403, 404, 409 |
| POST `/api/bookings/{id}/reschedule` | Customer (owner) | 200 | 400, 401, 403, 404, 409 |

### POST /api/auth/register
Request (`role` is `"Customer"` or `"Provider"`; password min 8 chars):
```json
{ "email": "ann@example.com", "password": "Passw0rd!", "name": "Ann", "role": "Customer" }
```
201:
```json
{ "token": "eyJhbGciOi...", "user": { "id": 1, "email": "ann@example.com", "name": "Ann", "role": "Customer" } }
```

### POST /api/auth/login
Request `{ "email": "ann@example.com", "password": "Passw0rd!" }` -> 200, same body as register. Bad credentials: 401 with generic detail.

### GET /api/resources
200:
```json
[ { "id": 10, "name": "Court 1", "description": "Indoor tennis court", "providerName": "Sports Co" } ]
```

### GET /api/resources/{id}/slots?from=2026-11-01T00:00:00Z&to=2026-11-08T00:00:00Z
`from` defaults to now, `to` is optional; `from` in the past is clamped to now. Returns only future slots with no active booking, ordered by `startUtc`. `from > to` gives 400.
200:
```json
[ { "id": 100, "startUtc": "2026-11-01T10:00:00Z", "endUtc": "2026-11-01T11:00:00Z", "priceCents": 2500 } ]
```

### POST /api/provider/resources
Request `{ "name": "Court 1", "description": "Indoor tennis court" }` (name required, max 200). 201:
```json
{ "id": 10, "name": "Court 1", "description": "Indoor tennis court" }
```

### GET /api/provider/resources
200: array of the same resource objects (own only).

### POST /api/provider/resources/{id}/slots
Request (`endUtc > startUtc`, `priceCents >= 0`):
```json
{ "startUtc": "2026-11-01T10:00:00Z", "endUtc": "2026-11-01T11:00:00Z", "priceCents": 2500 }
```
201: `{ "id": 100, "startUtc": "...", "endUtc": "...", "priceCents": 2500 }`. Overlap with an existing slot of the same resource: 409. Resource not owned: 404.

### DELETE /api/provider/slots/{id}
204 no body. 409 if the slot has any booking (active or historical).

### GET /api/provider/bookings
200: bookings on the provider's resources, each with the customer name:
```json
[ { "id": 500, "slotId": 100, "resourceName": "Court 1", "startUtc": "2026-11-01T10:00:00Z",
    "endUtc": "2026-11-01T11:00:00Z", "status": "Confirmed", "amountCents": 2500, "customerName": "Ann" } ]
```

### POST /api/bookings
Headers: `Idempotency-Key: 3f1c...`. Request (card token `fail` forces a decline in the mock):
```json
{ "slotId": 100, "cardToken": "tok_visa" }
```
201:
```json
{ "id": 500, "slotId": 100, "resourceName": "Court 1", "startUtc": "2026-11-01T10:00:00Z",
  "endUtc": "2026-11-01T11:00:00Z", "status": "Confirmed", "amountCents": 2500 }
```
Errors: 400 (missing key/body), 404 (slot not found), 409 (slot taken or already started), 402 (payment failed; booking is `PaymentFailed`, slot released):
```json
{ "type": "https://httpstatuses.io/402", "title": "Payment failed", "status": 402, "detail": "The card was declined." }
```

### GET /api/bookings
200: own bookings (all statuses), newest first, same booking shape as above.

### POST /api/bookings/{id}/cancel
No body. 200: the booking with `status: "Cancelled"`. 409 if not `Confirmed` (including already cancelled) or the slot has started. Refund is mocked.

### POST /api/bookings/{id}/reschedule
Request `{ "newSlotId": 101 }`. Atomic: the old booking becomes `Cancelled` and a **new booking with a new id** is created `Confirmed`. 200 returns the new booking. 400 if the new slot is on a different resource; 404 if the booking or slot is not found; 409 if the new slot is taken, in the past, the same slot, or the original is not `Confirmed`. On any failure the original booking is untouched. Price differences are ignored (no charge or refund).

## TypeScript types (frontend)

```ts
export type Role = 'Customer' | 'Provider';
export type BookingStatus = 'Pending' | 'Confirmed' | 'PaymentFailed' | 'Cancelled';

export interface User { id: number; email: string; name: string; role: Role }
export interface AuthResponse { token: string; user: User }
export interface RegisterRequest { email: string; password: string; name: string; role: Role }
export interface LoginRequest { email: string; password: string }

export interface Resource { id: number; name: string; description: string; providerName?: string }
export interface CreateResourceRequest { name: string; description: string }

export interface Slot { id: number; startUtc: string; endUtc: string; priceCents: number }
export interface CreateSlotRequest { startUtc: string; endUtc: string; priceCents: number }

export interface Booking {
  id: number; slotId: number; resourceName: string;
  startUtc: string; endUtc: string; status: BookingStatus; amountCents: number;
  customerName?: string; // provider view only
}
export interface CreateBookingRequest { slotId: number; cardToken: string }
export interface RescheduleRequest { newSlotId: number }

export interface ProblemDetails {
  type?: string; title: string; status: number; detail?: string;
  traceId?: string; errors?: Record<string, string[]>;
}
```
Ids are numeric in this contract; if the backend uses GUIDs, change `number` to `string` here and in PRD §6 together.

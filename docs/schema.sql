-- REFERENCE ONLY. This file documents the schema. EF Core migrations (BookingsApi/Data/Migrations)
-- are the source of truth and create the database on startup; this file is no longer executed.
-- Booking system schema, PostgreSQL 16. All times timestamptz (UTC), money in integer cents.
-- Identifiers are quoted PascalCase to match EF Core / Npgsql conventions.

CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE "Users" (
    "Id"           uuid         NOT NULL DEFAULT gen_random_uuid(),
    "Email"        varchar(256) NOT NULL,
    "PasswordHash" text         NOT NULL,
    "Name"         varchar(200) NOT NULL,
    "Role"         varchar(16)  NOT NULL,
    "CreatedAt"    timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_Users_Role" CHECK ("Role" IN ('Customer','Provider'))
);
CREATE UNIQUE INDEX "UX_Users_Email" ON "Users" ("Email");

CREATE TABLE "Resources" (
    "Id"          uuid         NOT NULL DEFAULT gen_random_uuid(),
    "ProviderId"  uuid         NOT NULL,
    "Name"        varchar(200) NOT NULL,
    "Description" varchar(2000),
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "PK_Resources" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Resources_Users_ProviderId" FOREIGN KEY ("ProviderId")
        REFERENCES "Users" ("Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_Resources_ProviderId" ON "Resources" ("ProviderId");

CREATE TABLE "Slots" (
    "Id"         uuid        NOT NULL DEFAULT gen_random_uuid(),
    "ResourceId" uuid        NOT NULL,
    "StartUtc"   timestamptz NOT NULL,
    "EndUtc"     timestamptz NOT NULL,
    "PriceCents" integer     NOT NULL,
    CONSTRAINT "PK_Slots" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Slots_Resources_ResourceId" FOREIGN KEY ("ResourceId")
        REFERENCES "Resources" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_Slots_EndAfterStart" CHECK ("EndUtc" > "StartUtc"),
    CONSTRAINT "CK_Slots_Price" CHECK ("PriceCents" >= 0),
    -- No overlapping slots within a resource; half-open [start,end) so back-to-back slots are allowed.
    -- Violation: SqlState 23P01 (exclusion_violation) -> map to 409.
    CONSTRAINT "EX_Slots_NoOverlap" EXCLUDE USING gist
        ("ResourceId" WITH =, tstzrange("StartUtc", "EndUtc", '[)') WITH &&)
);
CREATE INDEX "IX_Slots_ResourceId_StartUtc" ON "Slots" ("ResourceId", "StartUtc");

CREATE TABLE "Bookings" (
    "Id"             uuid         NOT NULL DEFAULT gen_random_uuid(),
    "SlotId"         uuid         NOT NULL,
    "CustomerId"     uuid         NOT NULL,
    "Status"         varchar(16)  NOT NULL,
    "AmountCents"    integer      NOT NULL,
    "PaymentRef"     varchar(100),
    "IdempotencyKey" varchar(100) NOT NULL,
    "CreatedAt"      timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "PK_Bookings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Bookings_Slots_SlotId" FOREIGN KEY ("SlotId")
        REFERENCES "Slots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Bookings_Users_CustomerId" FOREIGN KEY ("CustomerId")
        REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "CK_Bookings_Status" CHECK ("Status" IN ('Pending','Confirmed','PaymentFailed','Cancelled')),
    CONSTRAINT "CK_Bookings_Amount" CHECK ("AmountCents" >= 0)
);

-- No-overbooking guarantee: at most one active booking per slot. Violation: SqlState 23505 -> 409.
CREATE UNIQUE INDEX "UX_Bookings_SlotId_Active" ON "Bookings" ("SlotId")
    WHERE "Status" IN ('Pending','Confirmed');

-- Idempotency (violation 23505 -> load and return the original booking).
CREATE UNIQUE INDEX "UX_Bookings_CustomerId_IdempotencyKey" ON "Bookings" ("CustomerId", "IdempotencyKey");

-- "My bookings", newest first.
CREATE INDEX "IX_Bookings_CustomerId_CreatedAt" ON "Bookings" ("CustomerId", "CreatedAt" DESC);

-- Optional (not in PRD): speeds FK check on slot delete and provider-bookings join across all statuses.
-- CREATE INDEX "IX_Bookings_SlotId" ON "Bookings" ("SlotId");

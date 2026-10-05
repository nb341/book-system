using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookingsApi.Data.Migrations
{
    /// <summary>
    /// Fake demo data (all logins use password "Passw0rd!"). Stable GUIDs so Down can remove exactly these rows.
    /// Slots are generated relative to the moment the migration is applied (next 14 days, UTC) and never overlap.
    /// </summary>
    public partial class SeedDemoData : Migration
    {
        // PasswordHasher<User> (Identity v3) hash of "Passw0rd!".
        private const string DemoPasswordHash =
            "AQAAAAIAAYagAAAAECji8JRv10Qw1Njgr6ocQOdVLBAs5ctmIhCZhZQmh6+UsYDlFrTRb8EgN6QYMOYXNg==";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO "Users" ("Id", "Email", "PasswordHash", "Name", "Role") VALUES
                ('11111111-1111-1111-1111-000000000001', 'provider1@demo.test', '{DemoPasswordHash}', 'Olivia Bennett', 'Provider'),
                ('11111111-1111-1111-1111-000000000002', 'provider2@demo.test', '{DemoPasswordHash}', 'Marcus Lee', 'Provider'),
                ('22222222-2222-2222-2222-000000000001', 'customer1@demo.test', '{DemoPasswordHash}', 'Priya Shah', 'Customer'),
                ('22222222-2222-2222-2222-000000000002', 'customer2@demo.test', '{DemoPasswordHash}', 'Tom Alvarez', 'Customer'),
                ('22222222-2222-2222-2222-000000000003', 'customer3@demo.test', '{DemoPasswordHash}', 'Hannah Okafor', 'Customer');
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Resources" ("Id", "ProviderId", "Name", "Description") VALUES
                ('33333333-3333-3333-3333-000000000001', '11111111-1111-1111-1111-000000000001', 'Tennis Court A',
                 'Floodlit outdoor hard court with racket and ball hire available at reception. Max 4 players.'),
                ('33333333-3333-3333-3333-000000000002', '11111111-1111-1111-1111-000000000001', 'Downtown Meeting Room',
                 'Bright meeting room for up to 8 people with a 65-inch display, whiteboard and fast Wi-Fi.'),
                ('33333333-3333-3333-3333-000000000003', '11111111-1111-1111-1111-000000000002', 'Massage Therapy with Dana',
                 'One-hour deep tissue or relaxation massage with a licensed therapist. Oils and towels provided.'),
                ('33333333-3333-3333-3333-000000000004', '11111111-1111-1111-1111-000000000002', 'Loft Photo Studio',
                 'Natural-light loft studio with backdrops, strobes and a changing area. Great for portraits and product shots.');
                """);

            // 3 one-hour slots per day (09:00, 11:00, 15:00 UTC) for each of the next 14 days.
            // Ids are deterministic: 44444444-<resource>-4000-8000-<day*100+hour>.
            migrationBuilder.Sql("""
                INSERT INTO "Slots" ("Id", "ResourceId", "StartUtc", "EndUtc", "PriceCents")
                SELECT
                    format('44444444-%s-4000-8000-%s', lpad(r.n::text, 4, '0'), lpad((d.n * 100 + h.n)::text, 12, '0'))::uuid,
                    format('33333333-3333-3333-3333-%s', lpad(r.n::text, 12, '0'))::uuid,
                    (date_trunc('day', now() AT TIME ZONE 'UTC') + make_interval(days => d.n, hours => h.n)) AT TIME ZONE 'UTC',
                    (date_trunc('day', now() AT TIME ZONE 'UTC') + make_interval(days => d.n, hours => h.n + 1)) AT TIME ZONE 'UTC',
                    CASE r.n WHEN 1 THEN 2500 WHEN 2 THEN 6000 WHEN 3 THEN 9000 ELSE 15000 END
                      + CASE h.n WHEN 9 THEN 0 WHEN 11 THEN 500 ELSE 1500 END
                      + CASE WHEN extract(isodow FROM date_trunc('day', now() AT TIME ZONE 'UTC') + make_interval(days => d.n)) >= 6 THEN 1000 ELSE 0 END
                FROM generate_series(1, 4) AS r(n)
                CROSS JOIN generate_series(1, 14) AS d(n)
                CROSS JOIN (VALUES (9), (11), (15)) AS h(n);
                """);

            // Bookings (amount = slot price). At most one active (Pending/Confirmed) booking per slot:
            // booking 5 (Cancelled) and booking 7 (Confirmed) share a slot, showing the partial unique index.
            migrationBuilder.Sql("""
                INSERT INTO "Bookings" ("Id", "SlotId", "CustomerId", "Status", "AmountCents", "PaymentRef", "IdempotencyKey")
                SELECT b.id::uuid, s."Id", b.customer::uuid, b.status, s."PriceCents", b.payref, b.idem
                FROM (VALUES
                    ('55555555-5555-5555-5555-000000000001', 1, 2, 9,  '22222222-2222-2222-2222-000000000001', 'Confirmed',     'mock_pay_demo_0001', 'demo-seed-0001'),
                    ('55555555-5555-5555-5555-000000000002', 2, 3, 11, '22222222-2222-2222-2222-000000000002', 'Confirmed',     'mock_pay_demo_0002', 'demo-seed-0002'),
                    ('55555555-5555-5555-5555-000000000003', 3, 2, 15, '22222222-2222-2222-2222-000000000003', 'Confirmed',     'mock_pay_demo_0003', 'demo-seed-0003'),
                    ('55555555-5555-5555-5555-000000000004', 4, 4, 9,  '22222222-2222-2222-2222-000000000001', 'Confirmed',     'mock_pay_demo_0004', 'demo-seed-0004'),
                    ('55555555-5555-5555-5555-000000000005', 1, 5, 11, '22222222-2222-2222-2222-000000000002', 'Cancelled',     'mock_pay_demo_0005', 'demo-seed-0005'),
                    ('55555555-5555-5555-5555-000000000006', 2, 6, 9,  '22222222-2222-2222-2222-000000000003', 'PaymentFailed', NULL,                 'demo-seed-0006'),
                    ('55555555-5555-5555-5555-000000000007', 1, 5, 11, '22222222-2222-2222-2222-000000000003', 'Confirmed',     'mock_pay_demo_0007', 'demo-seed-0007')
                ) AS b(id, res, day, hr, customer, status, payref, idem)
                JOIN "Slots" s ON s."Id" = format('44444444-%s-4000-8000-%s', lpad(b.res::text, 4, '0'), lpad((b.day * 100 + b.hr)::text, 12, '0'))::uuid;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Also removes any bookings made by the demo customers or on the demo slots, otherwise FKs block the delete.
            migrationBuilder.Sql("""
                DELETE FROM "Bookings"
                WHERE "CustomerId" IN (SELECT "Id" FROM "Users" WHERE "Email" LIKE '%@demo.test')
                   OR "SlotId" IN (SELECT "Id" FROM "Slots" WHERE "ResourceId" IN
                        ('33333333-3333-3333-3333-000000000001', '33333333-3333-3333-3333-000000000002',
                         '33333333-3333-3333-3333-000000000003', '33333333-3333-3333-3333-000000000004'));
                DELETE FROM "Slots" WHERE "ResourceId" IN
                    ('33333333-3333-3333-3333-000000000001', '33333333-3333-3333-3333-000000000002',
                     '33333333-3333-3333-3333-000000000003', '33333333-3333-3333-3333-000000000004');
                DELETE FROM "Resources" WHERE "Id" IN
                    ('33333333-3333-3333-3333-000000000001', '33333333-3333-3333-3333-000000000002',
                     '33333333-3333-3333-3333-000000000003', '33333333-3333-3333-3333-000000000004');
                DELETE FROM "Users" WHERE "Id" IN
                    ('11111111-1111-1111-1111-000000000001', '11111111-1111-1111-1111-000000000002',
                     '22222222-2222-2222-2222-000000000001', '22222222-2222-2222-2222-000000000002',
                     '22222222-2222-2222-2222-000000000003');
                """);
        }
    }
}

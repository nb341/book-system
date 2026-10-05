using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookingsApi.Data;
using BookingsApi.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BookingsApi.Tests;

public sealed record TestUser(Guid Id, string Token);

public sealed record BookingResponse(Guid Id, Guid SlotId, Guid ResourceId, string ResourceName, DateTime StartUtc, DateTime EndUtc, string Status, int AmountCents);

/// <summary>Creates a throwaway Postgres database (migrated by the API on startup) and drops it on dispose.</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private const string AdminConnection = "Host=localhost;Port=5433;Database=postgres;Username=bookings;Password=bookings";
    private readonly string _dbName = $"bookings_test_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;
    private int _counter;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await ExecuteAdminAsync($"CREATE DATABASE \"{_dbName}\"");
        var cs = $"Host=localhost;Port=5433;Database={_dbName};Username=bookings;Password=bookings;Maximum Pool Size=100";
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("environment", "Development"); // applies migrations on startup
            b.UseSetting("ConnectionStrings:Default", cs);
        });
        _ = _factory.Server; // start host (runs migrations)
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)");
    }

    private static async Task ExecuteAdminAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(AdminConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public HttpClient CreateClient(string? token = null)
    {
        var c = _factory.CreateClient();
        if (token is not null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public async Task<TestUser> RegisterAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}{Interlocked.Increment(ref _counter)}_{Guid.NewGuid():N}@test.local";
        using var c = CreateClient();
        var res = await c.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Passw0rd!", name = "Test " + role, role });
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        return new TestUser(doc.GetProperty("user").GetProperty("id").GetGuid(), doc.GetProperty("token").GetString()!);
    }

    /// <summary>Creates a provider, a fresh resource and one future slot via the API; returns the slot id.</summary>
    public async Task<Guid> CreateSlotAsync(int priceCents = 2500)
    {
        var provider = await RegisterAsync("Provider");
        using var c = CreateClient(provider.Token);
        var r = await c.PostAsJsonAsync("/api/provider/resources",
            new { name = "Court " + Guid.NewGuid().ToString("N")[..6], description = "test" });
        r.EnsureSuccessStatusCode();
        var resourceId = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var start = DateTime.UtcNow.AddDays(30).Date.AddHours(10);
        var s = await c.PostAsJsonAsync($"/api/provider/resources/{resourceId}/slots",
            new { startUtc = start, endUtc = start.AddHours(1), priceCents });
        s.EnsureSuccessStatusCode();
        return (await s.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Creates a fresh resource owned by <paramref name="provider"/> with <paramref name="count"/> future slots.</summary>
    public async Task<(Guid ResourceId, List<Guid> SlotIds)> CreateResourceWithSlotsAsync(TestUser provider, int count)
    {
        using var c = CreateClient(provider.Token);
        var r = await c.PostAsJsonAsync("/api/provider/resources",
            new { name = "Court " + Guid.NewGuid().ToString("N")[..6], description = "test" });
        r.EnsureSuccessStatusCode();
        var resourceId = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var start = DateTime.UtcNow.AddDays(30 + i).Date.AddHours(10);
            var s = await c.PostAsJsonAsync($"/api/provider/resources/{resourceId}/slots",
                new { startUtc = start, endUtc = start.AddHours(1), priceCents = 2500 });
            s.EnsureSuccessStatusCode();
            ids.Add((await s.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }
        return (resourceId, ids);
    }

    /// <summary>Inserts a Confirmed booking on a slot that already started.</summary>
    public async Task<Guid> CreatePastConfirmedBookingAsync(TestUser customer)
    {
        var slotId = await CreatePastSlotAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            SlotId = slotId,
            CustomerId = customer.Id,
            Status = BookingStatus.Confirmed,
            AmountCents = 1000,
            PaymentRef = "mock_x",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    public async Task<HttpResponseMessage> PostAsync(TestUser user, string url, object? body = null)
    {
        using var c = CreateClient(user.Token);
        return await c.PostAsJsonAsync(url, body ?? new { });
    }

    public async Task<BookingResponse> BookOkAsync(TestUser user, Guid slotId)
    {
        using var res = await BookAsync(user, slotId, Guid.NewGuid().ToString("N"));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<BookingResponse>(Json))!;
    }

    /// <summary>Inserts a slot that already started (cannot be created through the API).</summary>
    public async Task<Guid> CreatePastSlotAsync()
    {
        var provider = await RegisterAsync("Provider");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resource = new Resource { Id = Guid.NewGuid(), ProviderId = provider.Id, Name = "Past court", CreatedAt = DateTime.UtcNow };
        var slot = new Slot
        {
            Id = Guid.NewGuid(),
            ResourceId = resource.Id,
            StartUtc = DateTime.UtcNow.AddHours(-3),
            EndUtc = DateTime.UtcNow.AddHours(-2),
            PriceCents = 1000
        };
        db.Resources.Add(resource);
        db.Slots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    public async Task<HttpResponseMessage> BookAsync(TestUser user, Guid slotId, string? key, string cardToken = "tok_visa")
    {
        using var c = CreateClient(user.Token);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new { slotId, cardToken })
        };
        if (key is not null) req.Headers.Add("Idempotency-Key", key);
        return await c.SendAsync(req);
    }

    public async Task<List<Booking>> BookingsForSlotAsync(Guid slotId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.AsNoTracking().Where(b => b.SlotId == slotId).ToListAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;

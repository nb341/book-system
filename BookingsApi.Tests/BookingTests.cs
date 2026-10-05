using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingsApi.Domain;

namespace BookingsApi.Tests;

[Collection("api")]
public sealed class BookingTests(ApiFixture api)
{
    [Fact]
    public async Task Parallel_bookings_on_one_slot_yield_exactly_one_success()
    {
        const int n = 20;
        var slotId = await api.CreateSlotAsync();
        var customers = new List<TestUser>();
        for (var i = 0; i < n; i++) customers.Add(await api.RegisterAsync("Customer"));

        using var gate = new ManualResetEventSlim(false);
        var tasks = customers.Select(u => Task.Run(async () =>
        {
            gate.Wait();
            using var res = await api.BookAsync(u, slotId, Guid.NewGuid().ToString("N"));
            return res.StatusCode;
        })).ToList();
        gate.Set();
        var codes = await Task.WhenAll(tasks);

        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.Created));
        Assert.Equal(n - 1, codes.Count(c => c == HttpStatusCode.Conflict));
        var rows = await api.BookingsForSlotAsync(slotId);
        var active = rows.Where(b => b.Status is BookingStatus.Pending or BookingStatus.Confirmed).ToList();
        Assert.Single(active);
        Assert.Equal(BookingStatus.Confirmed, active[0].Status);
        Assert.Single(rows); // losers never leave rows behind
    }

    [Fact]
    public async Task Payment_failure_returns_402_and_releases_the_slot()
    {
        var slotId = await api.CreateSlotAsync();
        var a = await api.RegisterAsync("Customer");
        var b = await api.RegisterAsync("Customer");

        using var res = await api.BookAsync(a, slotId, "k-fail-1", "fail");
        Assert.Equal(HttpStatusCode.PaymentRequired, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Payment failed", problem.GetProperty("title").GetString());
        Assert.Equal("The card was declined.", problem.GetProperty("detail").GetString());

        var rows = await api.BookingsForSlotAsync(slotId);
        Assert.Equal(BookingStatus.PaymentFailed, Assert.Single(rows).Status);

        using var ca = api.CreateClient(a.Token);
        var list = await ca.GetFromJsonAsync<List<BookingResponse>>("/api/bookings", ApiFixture.Json);
        Assert.Equal("PaymentFailed", Assert.Single(list!).Status);

        // The slot is listed as available again.
        using var cb = api.CreateClient(b.Token);
        var resources = await cb.GetFromJsonAsync<JsonElement>("/api/resources");
        var found = false;
        foreach (var r in resources.EnumerateArray())
        {
            var id = r.GetProperty("id").GetGuid();
            var slots = await cb.GetFromJsonAsync<JsonElement>($"/api/resources/{id}/slots");
            if (slots.EnumerateArray().Any(s => s.GetProperty("id").GetGuid() == slotId)) found = true;
        }
        Assert.True(found, "Released slot should appear in available slots.");

        using var res2 = await api.BookAsync(b, slotId, "k-ok-1");
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);
        var booking = await res2.Content.ReadFromJsonAsync<BookingResponse>(ApiFixture.Json);
        Assert.Equal("Confirmed", booking!.Status);
    }

    [Fact]
    public async Task Idempotent_replay_returns_same_booking_without_second_row()
    {
        var slotId = await api.CreateSlotAsync();
        var u = await api.RegisterAsync("Customer");
        var key = Guid.NewGuid().ToString("N");

        using var first = await api.BookAsync(u, slotId, key);
        using var second = await api.BookAsync(u, slotId, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var b1 = await first.Content.ReadFromJsonAsync<BookingResponse>(ApiFixture.Json);
        var b2 = await second.Content.ReadFromJsonAsync<BookingResponse>(ApiFixture.Json);
        Assert.Equal(b1!.Id, b2!.Id);
        Assert.Single(await api.BookingsForSlotAsync(slotId));
    }

    [Fact]
    public async Task Concurrent_duplicate_key_resolves_to_one_booking()
    {
        var slotId = await api.CreateSlotAsync();
        var u = await api.RegisterAsync("Customer");
        var key = Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
        {
            using var r = await api.BookAsync(u, slotId, key);
            return r.StatusCode;
        })));

        Assert.All(responses, c => Assert.Equal(HttpStatusCode.Created, c));
        Assert.Single(await api.BookingsForSlotAsync(slotId));
    }

    [Fact]
    public async Task Replay_of_failed_key_returns_402_again()
    {
        var slotId = await api.CreateSlotAsync();
        var u = await api.RegisterAsync("Customer");

        using var first = await api.BookAsync(u, slotId, "k-failed", "fail");
        using var second = await api.BookAsync(u, slotId, "k-failed", "fail");
        Assert.Equal(HttpStatusCode.PaymentRequired, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Single(await api.BookingsForSlotAsync(slotId));
    }

    [Fact]
    public async Task Past_slot_returns_409()
    {
        var slotId = await api.CreatePastSlotAsync();
        var u = await api.RegisterAsync("Customer");
        using var res = await api.BookAsync(u, slotId, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Empty(await api.BookingsForSlotAsync(slotId));
    }

    [Fact]
    public async Task Unknown_slot_returns_404()
    {
        var u = await api.RegisterAsync("Customer");
        using var res = await api.BookAsync(u, Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Missing_or_too_long_idempotency_key_returns_400()
    {
        var slotId = await api.CreateSlotAsync();
        var u = await api.RegisterAsync("Customer");
        using var missing = await api.BookAsync(u, slotId, null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var tooLong = await api.BookAsync(u, slotId, new string('x', 65));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Empty(await api.BookingsForSlotAsync(slotId));
    }

    [Fact]
    public async Task Provider_gets_403_on_bookings_endpoints()
    {
        var slotId = await api.CreateSlotAsync();
        var p = await api.RegisterAsync("Provider");
        using var res = await api.BookAsync(p, slotId, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        using var c = api.CreateClient(p.Token);
        using var list = await c.GetAsync("/api/bookings");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task List_returns_own_bookings_newest_first()
    {
        var s1 = await api.CreateSlotAsync();
        var s2 = await api.CreateSlotAsync();
        var u = await api.RegisterAsync("Customer");
        var other = await api.RegisterAsync("Customer");
        (await api.BookAsync(u, s1, "l1")).Dispose();
        (await api.BookAsync(u, s2, "l2")).Dispose();
        var s3 = await api.CreateSlotAsync();
        (await api.BookAsync(other, s3, "l3")).Dispose();

        using var c = api.CreateClient(u.Token);
        var list = await c.GetFromJsonAsync<List<BookingResponse>>("/api/bookings", ApiFixture.Json);
        Assert.Equal(new[] { s2, s1 }, list!.Select(b => b.SlotId).ToArray());
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingsApi.Domain;

namespace BookingsApi.Tests;

[Collection("api")]
public sealed class CancelRescheduleTests(ApiFixture api)
{
    private static async Task<BookingResponse> ReadAsync(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<BookingResponse>(ApiFixture.Json))!;

    [Fact]
    public async Task Cancel_own_confirmed_booking_frees_slot_for_others()
    {
        var slotId = await api.CreateSlotAsync();
        var a = await api.RegisterAsync("Customer");
        var b = await api.RegisterAsync("Customer");
        var booking = await api.BookOkAsync(a, slotId);

        using var res = await api.PostAsync(a, $"/api/bookings/{booking.Id}/cancel");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cancelled = await ReadAsync(res);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(booking.ResourceId, cancelled.ResourceId);

        using var rebook = await api.BookAsync(b, slotId, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);
    }

    [Fact]
    public async Task Cancel_twice_returns_409()
    {
        var slotId = await api.CreateSlotAsync();
        var a = await api.RegisterAsync("Customer");
        var booking = await api.BookOkAsync(a, slotId);
        using var first = await api.PostAsync(a, $"/api/bookings/{booking.Id}/cancel");
        using var second = await api.PostAsync(a, $"/api/bookings/{booking.Id}/cancel");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Cancel_someone_elses_booking_returns_404()
    {
        var slotId = await api.CreateSlotAsync();
        var a = await api.RegisterAsync("Customer");
        var other = await api.RegisterAsync("Customer");
        var booking = await api.BookOkAsync(a, slotId);
        using var res = await api.PostAsync(other, $"/api/bookings/{booking.Id}/cancel");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(BookingStatus.Confirmed, Assert.Single(await api.BookingsForSlotAsync(slotId)).Status);
    }

    [Fact]
    public async Task Cancel_payment_failed_booking_returns_409()
    {
        var slotId = await api.CreateSlotAsync();
        var a = await api.RegisterAsync("Customer");
        using var failed = await api.BookAsync(a, slotId, "k-pf", "fail");
        Assert.Equal(HttpStatusCode.PaymentRequired, failed.StatusCode);
        var row = Assert.Single(await api.BookingsForSlotAsync(slotId));
        using var res = await api.PostAsync(a, $"/api/bookings/{row.Id}/cancel");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Cancel_past_booking_returns_409()
    {
        var a = await api.RegisterAsync("Customer");
        var id = await api.CreatePastConfirmedBookingAsync(a);
        using var res = await api.PostAsync(a, $"/api/bookings/{id}/cancel");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Reschedule_happy_path_moves_booking_and_frees_old_slot()
    {
        var provider = await api.RegisterAsync("Provider");
        var (_, slots) = await api.CreateResourceWithSlotsAsync(provider, 2);
        var a = await api.RegisterAsync("Customer");
        var b = await api.RegisterAsync("Customer");
        var old = await api.BookOkAsync(a, slots[0]);

        using var res = await api.PostAsync(a, $"/api/bookings/{old.Id}/reschedule", new { newSlotId = slots[1] });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var moved = await ReadAsync(res);
        Assert.NotEqual(old.Id, moved.Id);
        Assert.Equal("Confirmed", moved.Status);
        Assert.Equal(slots[1], moved.SlotId);
        Assert.Equal(old.AmountCents, moved.AmountCents);

        var oldRows = await api.BookingsForSlotAsync(slots[0]);
        Assert.Equal(BookingStatus.Cancelled, Assert.Single(oldRows).Status);

        using var rebook = await api.BookAsync(b, slots[0], Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Created, rebook.StatusCode);

        // The moved booking can be rescheduled again (back to a free slot) without key collisions.
        using var c = api.CreateClient(a.Token);
        var list = await c.GetFromJsonAsync<List<BookingResponse>>("/api/bookings", ApiFixture.Json);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Reschedule_to_taken_slot_returns_409_and_keeps_original()
    {
        var provider = await api.RegisterAsync("Provider");
        var (_, slots) = await api.CreateResourceWithSlotsAsync(provider, 2);
        var a = await api.RegisterAsync("Customer");
        var b = await api.RegisterAsync("Customer");
        var mine = await api.BookOkAsync(a, slots[0]);
        await api.BookOkAsync(b, slots[1]);

        using var res = await api.PostAsync(a, $"/api/bookings/{mine.Id}/reschedule", new { newSlotId = slots[1] });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        var rows = await api.BookingsForSlotAsync(slots[0]);
        var row = Assert.Single(rows);
        Assert.Equal(mine.Id, row.Id);
        Assert.Equal(BookingStatus.Confirmed, row.Status);
        Assert.Single(await api.BookingsForSlotAsync(slots[1]));
    }

    [Fact]
    public async Task Reschedule_validation_errors()
    {
        var provider = await api.RegisterAsync("Provider");
        var (_, slots) = await api.CreateResourceWithSlotsAsync(provider, 1);
        var otherSlot = await api.CreateSlotAsync(); // different resource
        var a = await api.RegisterAsync("Customer");
        var other = await api.RegisterAsync("Customer");
        var mine = await api.BookOkAsync(a, slots[0]);

        using var diffResource = await api.PostAsync(a, $"/api/bookings/{mine.Id}/reschedule", new { newSlotId = otherSlot });
        Assert.Equal(HttpStatusCode.BadRequest, diffResource.StatusCode);

        using var same = await api.PostAsync(a, $"/api/bookings/{mine.Id}/reschedule", new { newSlotId = slots[0] });
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);

        using var notMine = await api.PostAsync(other, $"/api/bookings/{mine.Id}/reschedule", new { newSlotId = slots[0] });
        Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);

        using var unknown = await api.PostAsync(a, $"/api/bookings/{mine.Id}/reschedule", new { newSlotId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        Assert.Equal(BookingStatus.Confirmed, Assert.Single(await api.BookingsForSlotAsync(slots[0])).Status);
    }

    [Fact]
    public async Task Concurrent_reschedules_into_one_slot_yield_exactly_one_success()
    {
        var provider = await api.RegisterAsync("Provider");
        var (_, slots) = await api.CreateResourceWithSlotsAsync(provider, 3);
        var a = await api.RegisterAsync("Customer");
        var b = await api.RegisterAsync("Customer");
        var ba = await api.BookOkAsync(a, slots[0]);
        var bb = await api.BookOkAsync(b, slots[1]);

        using var gate = new ManualResetEventSlim(false);
        var tasks = new[] { (a, ba), (b, bb) }.Select(x => Task.Run(async () =>
        {
            gate.Wait();
            using var res = await api.PostAsync(x.Item1, $"/api/bookings/{x.Item2.Id}/reschedule", new { newSlotId = slots[2] });
            return res.StatusCode;
        })).ToList();
        gate.Set();
        var codes = await Task.WhenAll(tasks);

        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Equal(1, codes.Count(c => c == HttpStatusCode.Conflict));
        var target = await api.BookingsForSlotAsync(slots[2]);
        Assert.Equal(BookingStatus.Confirmed, Assert.Single(target).Status);

        // The loser's original booking is untouched.
        var loserSlot = target[0].CustomerId == a.Id ? slots[1] : slots[0];
        var loserRows = await api.BookingsForSlotAsync(loserSlot);
        Assert.Equal(BookingStatus.Confirmed, Assert.Single(loserRows).Status);
    }

    [Fact]
    public async Task Provider_bookings_scoped_to_own_resources()
    {
        var pa = await api.RegisterAsync("Provider");
        var pb = await api.RegisterAsync("Provider");
        var (resA, slotsA) = await api.CreateResourceWithSlotsAsync(pa, 1);
        var (_, slotsB) = await api.CreateResourceWithSlotsAsync(pb, 1);
        var customer = await api.RegisterAsync("Customer");
        var bookingA = await api.BookOkAsync(customer, slotsA[0]);
        var bookingB = await api.BookOkAsync(customer, slotsB[0]);

        using var ca = api.CreateClient(pa.Token);
        var doc = await ca.GetFromJsonAsync<JsonElement>("/api/provider/bookings");
        var item = Assert.Single(doc.EnumerateArray());
        Assert.Equal(bookingA.Id, item.GetProperty("id").GetGuid());
        Assert.Equal(resA, item.GetProperty("resourceId").GetGuid());
        Assert.Equal("Test Customer", item.GetProperty("customerName").GetString());
        Assert.Equal("Confirmed", item.GetProperty("status").GetString());
        Assert.DoesNotContain(doc.EnumerateArray(), e => e.GetProperty("id").GetGuid() == bookingB.Id);

        using var cc = api.CreateClient(customer.Token);
        using var forbidden = await cc.GetAsync("/api/provider/bookings");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookingsApi.Tests;

[Collection("api")]
public sealed class ResourceTests(ApiFixture api)
{
    private async Task<HttpResponseMessage> PutAsync(TestUser user, Guid resourceId, object body)
    {
        using var c = api.CreateClient(user.Token);
        return await c.PutAsJsonAsync($"/api/provider/resources/{resourceId}", body);
    }

    private async Task<HttpResponseMessage> DeleteAsync(TestUser user, Guid resourceId)
    {
        using var c = api.CreateClient(user.Token);
        return await c.DeleteAsync($"/api/provider/resources/{resourceId}");
    }

    private async Task<List<Guid>> OwnResourceIdsAsync(TestUser provider)
    {
        using var c = api.CreateClient(provider.Token);
        var list = await c.GetFromJsonAsync<List<JsonElement>>("/api/provider/resources");
        return list!.Select(r => r.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task Update_own_resource_returns_updated_resource()
    {
        var provider = await api.RegisterAsync("Provider");
        var (resourceId, _) = await api.CreateResourceWithSlotsAsync(provider, 0);

        using var res = await PutAsync(provider, resourceId, new { name = "  Renamed  ", description = "   " });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(resourceId, body.GetProperty("id").GetGuid());
        Assert.Equal("Renamed", body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("description").ValueKind);
    }

    [Fact]
    public async Task Update_validation_and_ownership()
    {
        var provider = await api.RegisterAsync("Provider");
        var other = await api.RegisterAsync("Provider");
        var customer = await api.RegisterAsync("Customer");
        var (resourceId, _) = await api.CreateResourceWithSlotsAsync(provider, 0);

        using var blank = await PutAsync(provider, resourceId, new { name = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        using var notOwned = await PutAsync(other, resourceId, new { name = "Hijack" });
        Assert.Equal(HttpStatusCode.NotFound, notOwned.StatusCode);
        using var missing = await PutAsync(provider, Guid.NewGuid(), new { name = "X" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var asCustomer = await PutAsync(customer, resourceId, new { name = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, asCustomer.StatusCode);
    }

    [Fact]
    public async Task Delete_resource_with_unbooked_slots_removes_it_and_its_slots()
    {
        var provider = await api.RegisterAsync("Provider");
        var (resourceId, _) = await api.CreateResourceWithSlotsAsync(provider, 3);

        using var res = await DeleteAsync(provider, resourceId);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.DoesNotContain(resourceId, await OwnResourceIdsAsync(provider));

        var customer = await api.RegisterAsync("Customer");
        using var c = api.CreateClient(customer.Token);
        using var slots = await c.GetAsync($"/api/resources/{resourceId}/slots");
        Assert.Equal(HttpStatusCode.NotFound, slots.StatusCode);
    }

    [Fact]
    public async Task Delete_resource_with_booking_history_returns_409()
    {
        var provider = await api.RegisterAsync("Provider");
        var customer = await api.RegisterAsync("Customer");
        var (resourceId, slotIds) = await api.CreateResourceWithSlotsAsync(provider, 2);
        var booking = await api.BookOkAsync(customer, slotIds[0]);
        using var cancel = await api.PostAsync(customer, $"/api/bookings/{booking.Id}/cancel");
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        // Even a cancelled booking is history that must keep its slot and resource.
        using var res = await DeleteAsync(provider, resourceId);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains(resourceId, await OwnResourceIdsAsync(provider));
    }

    [Fact]
    public async Task Delete_someone_elses_resource_returns_404()
    {
        var provider = await api.RegisterAsync("Provider");
        var other = await api.RegisterAsync("Provider");
        var (resourceId, _) = await api.CreateResourceWithSlotsAsync(provider, 1);

        using var res = await DeleteAsync(other, resourceId);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Contains(resourceId, await OwnResourceIdsAsync(provider));
    }
}

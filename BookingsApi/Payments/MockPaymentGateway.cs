namespace BookingsApi.Payments;

/// <summary>Mock gateway: card token "fail" is declined, anything else succeeds.</summary>
public sealed class MockPaymentGateway : IPaymentGateway
{
    public async Task<PaymentResult> ChargeAsync(int amountCents, string cardToken, Guid bookingId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        return string.Equals(cardToken, "fail", StringComparison.Ordinal)
            ? new PaymentResult(false, null, "The card was declined.")
            : new PaymentResult(true, $"mock_{Guid.NewGuid():N}", null);
    }
}

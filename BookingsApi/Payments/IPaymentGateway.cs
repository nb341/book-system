namespace BookingsApi.Payments;

public sealed record PaymentResult(bool Success, string? PaymentRef, string? FailureReason);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(int amountCents, string cardToken, Guid bookingId, CancellationToken ct);

    Task RefundAsync(string? paymentRef, int amountCents, CancellationToken ct);
}

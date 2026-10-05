namespace BookingsApi.Domain;

public enum Role
{
    Customer,
    Provider
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    PaymentFailed,
    Cancelled
}

namespace RestaurantReservationAndOrderManagementSystem.Services.Payments;

public interface IStripeCheckoutService
{
    Task<StripeCheckoutSession> CreateCheckoutSessionAsync(
        StripeCheckoutRequest request,
        CancellationToken cancellationToken = default);
}

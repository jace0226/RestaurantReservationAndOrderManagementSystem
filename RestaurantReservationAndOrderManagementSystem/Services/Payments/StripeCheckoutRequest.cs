namespace RestaurantReservationAndOrderManagementSystem.Services.Payments;

public sealed record StripeCheckoutRequest(
    IReadOnlyCollection<StripeCheckoutLineItem> LineItems,
    string SuccessUrl,
    string CancelUrl,
    string? CustomerEmail = null,
    string? ClientReferenceId = null);

public sealed record StripeCheckoutLineItem(
    string Name,
    long UnitAmountInMinorUnits,
    string Currency,
    long Quantity = 1);

public sealed record StripeCheckoutSession(string Id, string Url);

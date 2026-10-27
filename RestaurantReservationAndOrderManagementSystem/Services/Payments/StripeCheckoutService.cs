using Stripe;
using Stripe.Checkout;

namespace RestaurantReservationAndOrderManagementSystem.Services.Payments;

public sealed class StripeCheckoutService : IStripeCheckoutService
{
    private readonly StripeClient? _stripeClient;

    public StripeCheckoutService(IConfiguration configuration)
    {
        var secretKey = configuration["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            _stripeClient = new StripeClient(secretKey);
        }
    }

    public async Task<StripeCheckoutSession> CreateCheckoutSessionAsync(
        StripeCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var stripeClient = _stripeClient
            ?? throw new InvalidOperationException(
                "Stripe is not configured. Set the Stripe:SecretKey configuration value.");

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            CustomerEmail = request.CustomerEmail,
            ClientReferenceId = request.ClientReferenceId,
            LineItems = request.LineItems.Select(item => new SessionLineItemOptions
            {
                Quantity = item.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = item.Currency.ToLowerInvariant(),
                    UnitAmount = item.UnitAmountInMinorUnits,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = item.Name.Trim()
                    }
                }
            }).ToList()
        };

        var session = await new SessionService(stripeClient)
            .CreateAsync(options, cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException(
                $"Stripe created Checkout Session {session.Id} without a redirect URL.");
        }

        return new StripeCheckoutSession(session.Id, session.Url);
    }

    private static void ValidateRequest(StripeCheckoutRequest request)
    {
        if (request.LineItems is null || request.LineItems.Count == 0)
        {
            throw new ArgumentException(
                "At least one line item is required.",
                nameof(request));
        }

        if (!IsValidRedirectUrl(request.SuccessUrl) ||
            !IsValidRedirectUrl(request.CancelUrl))
        {
            throw new ArgumentException(
                "Success and cancel URLs must be absolute HTTP or HTTPS URLs.",
                nameof(request));
        }

        foreach (var item in request.LineItems)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                throw new ArgumentException(
                    "Each line item must have a name.",
                    nameof(request));
            }

            if (item.UnitAmountInMinorUnits <= 0 || item.Quantity <= 0)
            {
                throw new ArgumentException(
                    "Each line item must have a positive unit amount and quantity.",
                    nameof(request));
            }

            if (item.Currency is null ||
                item.Currency.Length != 3 ||
                !item.Currency.All(char.IsAsciiLetter))
            {
                throw new ArgumentException(
                    "Each line item must use a three-letter currency code.",
                    nameof(request));
            }
        }
    }

    private static bool IsValidRedirectUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

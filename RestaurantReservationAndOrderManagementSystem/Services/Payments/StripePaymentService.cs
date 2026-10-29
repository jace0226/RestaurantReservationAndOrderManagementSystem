using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Models;
using Stripe;
using Stripe.Checkout;

namespace RestaurantReservationAndOrderManagementSystem.Services.Payments;

public sealed class StripePaymentService
{
    private const string Currency = "myr";
    private readonly ApplicationDbContext _dbContext;
    private readonly PromotionService _promotionService;
    private readonly StripeClient? _stripeClient;

    public StripePaymentService(
        ApplicationDbContext dbContext,
        PromotionService promotionService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _promotionService = promotionService;
        var secretKey = configuration["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(secretKey))
        {
            _stripeClient = new StripeClient(secretKey);
        }
    }

    public bool IsConfigured => _stripeClient is not null;

    public async Task<Session> CreateCheckoutSessionAsync(
        string orderId,
        string customerId,
        string customerEmail,
        IEnumerable<(string Name, decimal OriginalUnitPrice, decimal EffectiveUnitPrice, int Quantity, bool HasPromotion, decimal DiscountAmount)> items,
        string successUrl,
        string cancelUrl)
    {
        var lineItems = items.ToList();
        var originalSubtotal = lineItems.Sum(item => item.OriginalUnitPrice * item.Quantity);
        var netSubtotal = lineItems.Sum(item => item.EffectiveUnitPrice * item.Quantity);
        var totalDiscount = Math.Max(0, originalSubtotal - netSubtotal);
        var tax = decimal.Round(netSubtotal * 0.10m, 2, MidpointRounding.AwayFromZero);
        var total = netSubtotal + tax;

        var stripeLineItems = lineItems.Select(item => new SessionLineItemOptions
        {
            Quantity = item.Quantity,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = Currency,
                UnitAmount = ToMinorUnits(item.EffectiveUnitPrice),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = item.HasPromotion ? $"{item.Name} [Promo Applied]" : item.Name,
                    Description = item.HasPromotion
                        ? $"Promotional item: regular price RM {item.OriginalUnitPrice:N2} (Saved RM {item.DiscountAmount:N2})"
                        : null
                }
            }
        }).ToList();

        if (tax > 0)
        {
            stripeLineItems.Add(new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = Currency,
                    UnitAmount = ToMinorUnits(tax),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Tax (10%)"
                    }
                }
            });
        }

        var checkoutTotal = stripeLineItems.Sum(item =>
            item.PriceData.UnitAmount.GetValueOrDefault() * item.Quantity.GetValueOrDefault());
        if (checkoutTotal != ToMinorUnits(total))
        {
            throw new InvalidOperationException("The order total cannot be represented exactly in MYR minor units.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["OrderID"] = orderId,
            ["CustomerID"] = customerId,
            ["OriginalSubtotal"] = originalSubtotal.ToString("F2"),
            ["TotalDiscount"] = totalDiscount.ToString("F2"),
            ["PromoApplied"] = totalDiscount > 0 ? "true" : "false",
            ["NetSubtotal"] = netSubtotal.ToString("F2"),
            ["Tax"] = tax.ToString("F2"),
            ["Total"] = total.ToString("F2")
        };

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = customerEmail,
            ClientReferenceId = orderId,
            LineItems = stripeLineItems,
            Metadata = metadata,
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata,
                ReceiptEmail = customerEmail
            }
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"checkout:{orderId}:{ToMinorUnits(total).ToString(CultureInfo.InvariantCulture)}"
        };

        var session = await new SessionService(GetStripeClient())
            .CreateAsync(options, requestOptions);
        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException(
                $"Stripe created Checkout Session {session.Id} without a redirect URL.");
        }

        return session;
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        string orderId,
        string customerId,
        string customerEmail,
        IEnumerable<(string Name, decimal UnitPrice, int Quantity)> items,
        string successUrl,
        string cancelUrl)
    {
        return await CreateCheckoutSessionAsync(
            orderId,
            customerId,
            customerEmail,
            items.Select(i => (i.Name, i.UnitPrice, i.UnitPrice, i.Quantity, false, 0m)),
            successUrl,
            cancelUrl);
    }

    public async Task<Session> GetCheckoutSessionAsync(string sessionId) =>
        await new SessionService(GetStripeClient()).GetAsync(sessionId);

    public async Task<PaymentIntent> CreatePaymentIntentAsync(
        string orderId,
        string customerId,
        string customerEmail,
        decimal total)
    {
        var stripeClient = GetStripeClient();
        var amountInMinorUnits = ToMinorUnits(total);
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountInMinorUnits,
            Currency = Currency,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            Metadata = new Dictionary<string, string>
            {
                ["OrderID"] = orderId,
                ["CustomerID"] = customerId
            },
            ReceiptEmail = customerEmail
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"payment-intent:{orderId}:{amountInMinorUnits.ToString(CultureInfo.InvariantCulture)}"
        };

        return await new PaymentIntentService(stripeClient)
            .CreateAsync(options, requestOptions);
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId) =>
        await new PaymentIntentService(GetStripeClient()).GetAsync(paymentIntentId);

    public async Task CompleteSucceededPaymentAsync(PaymentIntent paymentIntent)
    {
        ArgumentNullException.ThrowIfNull(paymentIntent);
        if (paymentIntent.Status != "succeeded")
        {
            throw new InvalidOperationException("Only succeeded PaymentIntents can complete an order.");
        }

        if (!paymentIntent.Metadata.TryGetValue("OrderID", out var orderId) ||
            string.IsNullOrWhiteSpace(orderId) ||
            !paymentIntent.Currency.Equals(Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The PaymentIntent is missing valid order metadata or currency.");
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OrderID == orderId)
            ?? throw new InvalidOperationException("The PaymentIntent references an unknown order.");
        var items = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.OrderID == orderId)
            .Include(item => item.Food)
            .ToListAsync();

        var paidAt = DateTime.Now;
        decimal? expectedRecordedAmount = paymentIntent.Amount == 0
            ? null
            : paymentIntent.Amount / 100m;
        var pricing = await _promotionService.ResolvePaymentAlignedPricingAsync(
            items.Select(item => (item.Food, item.Quantity)),
            paidAt,
            expectedRecordedAmount);

        if (paymentIntent.Amount != ToMinorUnits(pricing.Total))
        {
            throw new InvalidOperationException("The PaymentIntent amount does not match the order total.");
        }

        var paymentMethod = await GetPaymentMethodDescriptionAsync(paymentIntent);

        await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

        var existingPayment = await _dbContext.Transactions
            .SingleOrDefaultAsync(item => item.OrderID == orderId);
        if (existingPayment is not null)
        {
            if (existingPayment.TransactionID != paymentIntent.Id)
            {
                throw new InvalidOperationException("A different payment has already been recorded for this order.");
            }

            await databaseTransaction.CommitAsync();
            return;
        }

        var currentOrder = await _dbContext.Orders
            .SingleOrDefaultAsync(item => item.OrderID == orderId)
            ?? throw new InvalidOperationException("The PaymentIntent references an unknown order.");
        if (currentOrder.OrderStatus != "Payment")
        {
            throw new InvalidOperationException("The order is not awaiting payment.");
        }

        currentOrder.OrderStatus = "Completed";
        currentOrder.CompletedAt = paidAt;
        _dbContext.Transactions.Add(new Transaction
        {
            TransactionID = paymentIntent.Id,
            OrderID = orderId,
            TransactionType = paymentMethod,
            TransactionDate = DateOnly.FromDateTime(paidAt),
            TransactionTime = TimeOnly.FromDateTime(paidAt),
            TransactionAmount = pricing.Total,
            OriginalSubtotal = pricing.OriginalSubtotal,
            DiscountAmount = pricing.TotalDiscount,
            NetSubtotal = pricing.NetSubtotal,
            TaxAmount = pricing.Tax
        });

        await _dbContext.SaveChangesAsync();
        await databaseTransaction.CommitAsync();
    }

    private async Task<string> GetPaymentMethodDescriptionAsync(PaymentIntent paymentIntent)
    {
        if (string.IsNullOrWhiteSpace(paymentIntent.PaymentMethodId))
        {
            return "Card";
        }

        var paymentMethod = await new PaymentMethodService(GetStripeClient())
            .GetAsync(paymentIntent.PaymentMethodId);
        if (paymentMethod.Card is null || string.IsNullOrWhiteSpace(paymentMethod.Card.Last4))
        {
            return "Card";
        }

        var brand = string.IsNullOrWhiteSpace(paymentMethod.Card.Brand)
            ? "Card"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(paymentMethod.Card.Brand);
        return $"{brand} ending in {paymentMethod.Card.Last4}";
    }

    private StripeClient GetStripeClient() =>
        _stripeClient ?? throw new InvalidOperationException(
            "Stripe is not configured. Set the Stripe:SecretKey configuration value.");

    private static long ToMinorUnits(decimal amount) =>
        checked((long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
}

public static class PaymentPricing
{
    public static PaymentAmounts Calculate(IEnumerable<(decimal UnitPrice, int Quantity)> items)
    {
        var subtotal = items.Sum(item => item.UnitPrice * item.Quantity);
        var tax = decimal.Round(subtotal * 0.10m, 2, MidpointRounding.AwayFromZero);
        return new PaymentAmounts(subtotal, tax, subtotal + tax);
    }
}

public sealed record PaymentAmounts(decimal Subtotal, decimal Tax, decimal Total);

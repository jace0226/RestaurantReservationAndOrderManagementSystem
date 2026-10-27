using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Services.Payments;
using Stripe;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Payment;

[Authorize(Roles = "Customer")]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly StripePaymentService _stripePaymentService;

    public IndexModel(ApplicationDbContext dbContext, StripePaymentService stripePaymentService)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
    }

    public ReceiptOrder Order { get; private set; } = null!;
    public ReceiptPayment Payment { get; private set; } = null!;
    public IReadOnlyList<ReceiptItem> Items { get; private set; } = [];
    public decimal Subtotal { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(
        string? orderId,
        [FromQuery(Name = "session_id")] string? sessionId,
        string? paymentIntentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest();
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(item => item.Customer)
            .SingleOrDefaultAsync(item =>
                item.OrderID == orderId &&
                item.Customer.UserID == userId);
        if (order is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var checkoutSession = await _stripePaymentService.GetCheckoutSessionAsync(sessionId);
            if (checkoutSession.PaymentStatus != "paid" ||
                !checkoutSession.Metadata.TryGetValue("OrderID", out var sessionOrderId) ||
                sessionOrderId != orderId ||
                !checkoutSession.Metadata.TryGetValue("CustomerID", out var sessionCustomerId) ||
                sessionCustomerId != order.CustomerID ||
                string.IsNullOrWhiteSpace(checkoutSession.PaymentIntentId))
            {
                return BadRequest("The checkout session could not be verified for this order.");
            }

            paymentIntentId = checkoutSession.PaymentIntentId;
        }

        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return BadRequest();
        }

        var paymentIntent = await _stripePaymentService.GetPaymentIntentAsync(paymentIntentId);
        if (paymentIntent.Status != "succeeded" ||
            !paymentIntent.Metadata.TryGetValue("OrderID", out var paymentOrderId) ||
            paymentOrderId != orderId ||
            !paymentIntent.Metadata.TryGetValue("CustomerID", out var paymentCustomerId) ||
            paymentCustomerId != order.CustomerID)
        {
            return BadRequest("The payment could not be verified for this order.");
        }

        await _stripePaymentService.CompleteSucceededPaymentAsync(paymentIntent);

        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.OrderID == orderId &&
                item.TransactionID == paymentIntentId);
        if (transaction is null)
        {
            return NotFound();
        }

        var orderItems = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.OrderID == orderId)
            .Include(item => item.Food)
            .ToListAsync();
        Items = orderItems.Select(item => new ReceiptItem(
            item.Food.FoodName,
            item.Quantity,
            item.Food.Price,
            item.Food.Price * item.Quantity)).ToList();

        var amounts = PaymentPricing.Calculate(Items.Select(item => (item.UnitPrice, item.Quantity)));
        Order = new ReceiptOrder(order.OrderID, order.CreatedAt);
        Payment = new ReceiptPayment(
            transaction.TransactionID,
            transaction.TransactionType,
            transaction.TransactionAmount);
        Subtotal = amounts.Subtotal;
        Tax = amounts.Tax;
        Total = transaction.TransactionAmount;
        InvoiceNumber = $"INV-{order.OrderID}-{order.CreatedAt:yyyyMMddHHmmss}";
        return Page();
    }

    public sealed record ReceiptOrder(string OrderID, DateTime OrderDateTime);
    public sealed record ReceiptPayment(string TransactionReference, string PaymentMethod, decimal Amount);
    public sealed record ReceiptItem(string Name, int Quantity, decimal UnitPrice, decimal LineTotal);
}
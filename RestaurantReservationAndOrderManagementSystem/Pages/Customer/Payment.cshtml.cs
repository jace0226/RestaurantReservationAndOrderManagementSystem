using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Data;
using RestaurantReservationAndOrderManagementSystem.Services.Payments;
using Stripe;

namespace RestaurantReservationAndOrderManagementSystem.Pages.Customer;

[Authorize(Roles = "Customer")]
public sealed class PaymentModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly StripePaymentService _stripePaymentService;
    private readonly ILogger<PaymentModel> _logger;

    public PaymentModel(
        ApplicationDbContext dbContext,
        StripePaymentService stripePaymentService,
        ILogger<PaymentModel> logger)
    {
        _dbContext = dbContext;
        _stripePaymentService = stripePaymentService;
        _logger = logger;
    }

    public string OrderId { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(orderId))
        {
            return NotFound();
        }

        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(item => item.Customer)
                .ThenInclude(customer => customer.User)
            .SingleOrDefaultAsync(item =>
                item.OrderID == orderId &&
                item.Customer.UserID == userId);

        if (order is null)
        {
            return NotFound();
        }

        if (order.OrderStatus != "Payment")
        {
            return BadRequest("This order is not awaiting payment.");
        }

        if (!_stripePaymentService.IsConfigured)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Stripe Checkout is not configured. Set Stripe:SecretKey before accepting payments.");
        }

        var orderItems = await _dbContext.OrderItems
            .AsNoTracking()
            .Where(item => item.OrderID == orderId)
            .Include(item => item.Food)
            .ToListAsync();
        if (orderItems.Count == 0)
        {
            return BadRequest("This order has no items to pay for.");
        }

        var checkoutItems = orderItems.Select(item => (
            Name: item.Food.FoodName,
            UnitPrice: item.Food.Price,
            Quantity: item.Quantity)).ToList();
        var amounts = PaymentPricing.Calculate(checkoutItems.Select(item => (item.UnitPrice, item.Quantity)));
        if (amounts.Total <= 0)
        {
            return BadRequest("This order has an invalid total.");
        }

        OrderId = order.OrderID;
        var successUrl = Url.Page(
            pageName: "/Payment/Index",
            pageHandler: null,
            values: new { orderId = order.OrderID },
            protocol: Request.Scheme)
            ?? throw new InvalidOperationException("Could not generate the payment receipt URL.");
        successUrl += "&session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Page(
            pageName: "/Customer/CustomerOrders",
            pageHandler: null,
            values: new { category = "Current" },
            protocol: Request.Scheme)
            ?? throw new InvalidOperationException("Could not generate the payment cancellation URL.");

        try
        {
            var session = await _stripePaymentService.CreateCheckoutSessionAsync(
                order.OrderID,
                order.CustomerID,
                order.Customer.User.Email,
                checkoutItems,
                successUrl,
                cancelUrl);
            return Redirect(session.Url);
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Could not create Stripe Checkout Session for order {OrderId}.", order.OrderID);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                "Stripe could not start checkout. Please try again or contact support.");
        }
    }
}

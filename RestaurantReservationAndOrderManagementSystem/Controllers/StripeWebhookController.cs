using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantReservationAndOrderManagementSystem.Services.Payments;
using Stripe;

namespace RestaurantReservationAndOrderManagementSystem.Controllers;

[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/stripe/webhook")]
public sealed class StripeWebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly StripePaymentService _stripePaymentService;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IConfiguration configuration,
        StripePaymentService stripePaymentService,
        ILogger<StripeWebhookController> logger)
    {
        _configuration = configuration;
        _stripePaymentService = stripePaymentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            _logger.LogError("Stripe webhook secret is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Stripe.Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                Request.Headers["Stripe-Signature"],
                webhookSecret);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "Stripe webhook signature validation failed.");
            return BadRequest();
        }

        if (stripeEvent.Type != EventTypes.PaymentIntentSucceeded)
        {
            return Ok();
        }

        if (stripeEvent.Data.Object is not PaymentIntent paymentIntent)
        {
            return BadRequest();
        }

        try
        {
            await _stripePaymentService.CompleteSucceededPaymentAsync(paymentIntent);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Could not persist successful Stripe payment {PaymentIntentId}.", paymentIntent.Id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        return Ok();
    }
}

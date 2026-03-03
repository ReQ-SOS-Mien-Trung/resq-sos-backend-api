using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Commands.CreateDonation;
using RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn;
using System.Text.Json;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/donations")]
[ApiController]
public class DonationController(IMediator mediator, IPaymentGatewayService paymentGatewayService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPaymentGatewayService _paymentGatewayService = paymentGatewayService;

    [HttpPost]
    [ProducesResponseType(typeof(CreateDonationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDonationRequestDto dto)
    {
        var command = new CreateDonationCommand
        {
            FundCampaignId = dto.FundCampaignId,
            DonorName = dto.DonorName,
            DonorEmail = dto.DonorEmail,
            Amount = dto.Amount,
            Note = dto.Note
        };

        var result = await _mediator.Send(command);
        return StatusCode(201, result);
    }

    [HttpPost("payment-return")]
    [AllowAnonymous] // Webhooks come from external PayOS servers without Bearer tokens
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPaymentReturn()
    {
        try
        {
            // 1. Read Raw Request Body
            // Do NOT use [FromBody] here, as it consumes the stream and deserializes implicitly
            using var reader = new StreamReader(Request.Body);
            var jsonBody = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(jsonBody))
            {
                return Ok(new { success = false, message = "Empty body" });
            }

            // 2. Verify Signature using the Raw JSON String
            // This prevents mismatch caused by field reordering or type conversion during binding
            var isValidSignature = _paymentGatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                // PayOS recommends 200 OK with success=false to acknowledge receipt but indicate logic failure
                return Ok(new { success = false, message = "Webhook signature mismatch" });
            }

            // 3. Deserialize safely now that signature is verified
            var webhookOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var webhookData = JsonSerializer.Deserialize<WebhookType>(jsonBody, webhookOptions);

            if (webhookData == null || webhookData.Data == null)
            {
                return Ok(new { success = false, message = "Invalid webhook data structure" });
            }

            // 4. Process Business Logic via MediatR
            var command = new ProcessPaymentReturnCommand { WebhookData = webhookData };
            var success = await _mediator.Send(command);

            if (success)
            {
                return Ok(new { success = true, message = "Payment updated successfully." });
            }

            return Ok(new { success = false, message = "Failed to process payment." });
        }
        catch (Exception)
        {
            // Log exception in a real scenario
            return Ok(new { success = false, message = "Internal Server Error" });
        }
    }
}
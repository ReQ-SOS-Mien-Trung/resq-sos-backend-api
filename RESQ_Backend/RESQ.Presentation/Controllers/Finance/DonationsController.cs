using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Commands.CreateDonation;
using RESQ.Application.UseCases.Finance.Commands.ProcessPaymentReturn;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;
using RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;
using System.Text.Json;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/donations")]
[ApiController]
public class DonationController(IMediator mediator, IPaymentGatewayService paymentGatewayService) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPaymentGatewayService _paymentGatewayService = paymentGatewayService;

    /// <summary>
    /// Lấy danh sách ủng hộ (Admin/Manager view) - Có thể xem cả ẩn danh nếu không filter
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDonations([FromQuery] GetDonationsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// API cho Landing Page: Chỉ lấy danh sách ủng hộ công khai (IsPrivate = false)
    /// </summary>
    [HttpGet("public")]
    public async Task<IActionResult> GetPublicDonations([FromQuery] GetPublicDonationsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Tạo yêu cầu ủng hộ mới và lấy link thanh toán
    /// </summary>
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

    /// <summary>
    /// Webhook xử lý kết quả thanh toán từ PayOS
    /// </summary>
    [HttpPost("payment-return")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPaymentReturn()
    {
        try
        {
            // 1. Read Raw Request Body
            // We use the raw stream to ensure the signature matches exactly what PayOS sent
            using var reader = new StreamReader(Request.Body);
            var jsonBody = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(jsonBody))
            {
                return Ok(new { success = false, message = "Empty body" });
            }

            // 2. Verify Signature
            var isValidSignature = _paymentGatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                // PayOS expects 200 OK even on logic errors to stop retrying
                return Ok(new { success = false, message = "Webhook signature mismatch" });
            }

            // 3. Deserialize
            var webhookOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var webhookData = JsonSerializer.Deserialize<WebhookType>(jsonBody, webhookOptions);

            if (webhookData == null || webhookData.Data == null)
            {
                return Ok(new { success = false, message = "Invalid webhook data structure" });
            }

            // 4. Process Logic
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
            // In production: _logger.LogError(ex, "Webhook processing failed");
            return Ok(new { success = false, message = "Internal Server Error" });
        }
    }
}

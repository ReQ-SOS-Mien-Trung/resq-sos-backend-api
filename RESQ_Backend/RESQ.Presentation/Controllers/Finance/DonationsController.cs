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
public class DonationController(IMediator mediator, IPaymentGatewayService paymentGatewayService, ILogger<DonationController> logger) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPaymentGatewayService _paymentGatewayService = paymentGatewayService;
    private readonly ILogger<DonationController> _logger = logger;

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
            // 1. Enable buffering to read the stream
            Request.EnableBuffering();

            string jsonBody;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            {
                jsonBody = await reader.ReadToEndAsync();
                // Reset position for later use if needed (though we deserialize string directly)
                Request.Body.Position = 0;
            }

            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return Ok(new { success = false, message = "Empty webhook payload." });
            }

            // 2. Verify Signature
            var isValidSignature = _paymentGatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                _logger.LogWarning("Webhook signature verification failed.");
                return Ok(new { success = false, message = "Webhook signature mismatch." });
            }

            // 3. Deserialize to Object
            var webhook = JsonSerializer.Deserialize<WebhookType>(jsonBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhook == null || webhook.Data == null)
            {
                return Ok(new { success = false, message = "Invalid webhook data." });
            }

            // 4. Process Payment Logic
            var command = new ProcessPaymentReturnCommand
            {
                WebhookData = webhook
            };

            var result = await _mediator.Send(command);

            if (!result)
            {
                _logger.LogWarning("Payment logic returned false, but returning 200 OK to acknowledge webhook.");
            }

            return Ok(new { success = true, message = "Webhook received." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            // Always return 200 to PayOS to stop retry spam, even on internal errors, 
            // unless we want them to retry. Usually we catch and log.
            return Ok(new { success = true, message = "Webhook received with internal error." });
        }
    }
}

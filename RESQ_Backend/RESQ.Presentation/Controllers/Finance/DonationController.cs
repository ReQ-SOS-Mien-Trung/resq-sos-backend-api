using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RESQ.Application.Common.Models.Finance.Momo;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Commands.CreateDonation;
using RESQ.Application.UseCases.Finance.Commands.ProcessMomoPayment;
using RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;
using RESQ.Application.UseCases.Finance.Queries.GetPaymentMethods;
using System.Text.Json.Serialization;
using RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;
using System.Text.Json;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/donations")]
[ApiController]
public class DonationController(IMediator mediator, IPaymentGatewayService paymentGatewayService, ILogger<DonationController> logger, IConfiguration configuration) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPaymentGatewayService _paymentGatewayService = paymentGatewayService;
    private readonly ILogger<DonationController> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    /// <summary>
    /// Lấy danh sách phương thức thanh toán
    /// </summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var result = await _mediator.Send(new GetPaymentMethodsQuery());
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách ủng hộ (Admin/Manager view)
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
    /// Tạo yêu cầu ủng hộ mới và lấy link thanh toán (PayOS hoặc MoMo)
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
            Note = dto.Note,
            IsPrivate = dto.IsPrivate,
            PaymentMethodId = dto.PaymentMethodId
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
    public async Task<IActionResult> ProcessPayosPaymentReturn()
    {
        try
        {
            Request.EnableBuffering();
            string jsonBody;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            {
                jsonBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return Ok(new { success = false, message = "Empty webhook payload." });
            }

            var isValidSignature = _paymentGatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                _logger.LogWarning("PayOS Webhook signature verification failed.");
                return Ok(new { success = false, message = "Webhook signature mismatch." });
            }

            var webhook = JsonSerializer.Deserialize<WebhookType>(jsonBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhook == null || webhook.Data == null)
            {
                return Ok(new { success = false, message = "Invalid webhook data." });
            }

            var command = new ProcessPayosPaymentReturnCommand
            {
                WebhookData = webhook
            };

            await _mediator.Send(command);
            return Ok(new { success = true, message = "Webhook received." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS webhook");
            return Ok(new { success = true, message = "Webhook received with internal error." });
        }
    }

    /// <summary>
    /// Webhook xử lý kết quả thanh toán từ MoMo
    /// </summary>
    [HttpPost("momo-ipn")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ProcessMomoIpn()
    {
        try
        {
            Request.EnableBuffering();
            string jsonBody;
            using (var reader = new StreamReader(Request.Body, leaveOpen: true))
            {
                jsonBody = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            _logger.LogInformation("Received MoMo IPN Webhook: {Payload}", jsonBody);

            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return NoContent();
            }

            var ipnData = JsonSerializer.Deserialize<MomoIpnRequest>(jsonBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            });

            if (ipnData == null)
            {
                _logger.LogWarning("MoMo IPN Webhook deserialized to null.");
                return NoContent();
            }

            var command = new ProcessMomoPaymentCommand
            {
                IpnData = ipnData
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return NoContent();
        }
    }

    /// <summary>
    /// Redirect User sau khi hoàn tất thanh toán MoMo
    /// </summary>
    [HttpGet("momo-return")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult MomoReturn([FromQuery] int resultCode)
    {
        var config = _configuration.GetSection("MomoAPI");
        if (resultCode == 0)
        {
            var returnUrl = config["ReturnUrl"];
            return Redirect(returnUrl ?? "http://localhost:5173/success");
        }
        else
        {
            var cancelUrl = config["CancelUrl"];
            return Redirect(cancelUrl ?? "http://localhost:5173/fail");
        }
    }
}


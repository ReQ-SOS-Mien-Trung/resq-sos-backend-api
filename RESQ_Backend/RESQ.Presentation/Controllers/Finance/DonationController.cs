using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models.Finance.Momo;
using RESQ.Application.Common.Models.Finance.ZaloPay;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Commands.CreateDonation;
using RESQ.Application.UseCases.Finance.Commands.ProcessMomoPayment;
using RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;
using RESQ.Application.UseCases.Finance.Commands.ProcessZaloPayPayment;
using RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;
using RESQ.Application.UseCases.Finance.Queries.GetPaymentMethods;
using RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;
using System.Text.Json;

namespace RESQ.Presentation.Controllers.Finance;

[Route("finance/donations")]
[ApiController]
public class DonationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPaymentGatewayFactory _paymentGatewayFactory;
    private readonly ILogger<DonationController> _logger;
    private readonly IConfiguration _configuration;

    public DonationController(IMediator mediator, IPaymentGatewayFactory paymentGatewayFactory, ILogger<DonationController> logger, IConfiguration configuration)
    {
        _mediator = mediator;
        _paymentGatewayFactory = paymentGatewayFactory;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var result = await _mediator.Send(new GetPaymentMethodsQuery());
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetDonations([FromQuery] GetDonationsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("public")]
    public async Task<IActionResult> GetPublicDonations([FromQuery] GetPublicDonationsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

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
                return Ok(new { success = false, message = "Dữ liệu webhook trống." });
            }

            // Retrieve PayOS service dynamically via Factory
            var gatewayService = _paymentGatewayFactory.GetService("PAYOS");
            var isValidSignature = gatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                _logger.LogWarning("PayOS Webhook signature verification failed.");
                return Ok(new { success = false, message = "Chữ ký webhook không hợp lệ." });
            }

            var webhook = JsonSerializer.Deserialize<WebhookType>(jsonBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhook == null || webhook.Data == null)
            {
                return Ok(new { success = false, message = "Dữ liệu webhook không hợp lệ." });
            }

            var command = new ProcessPayosPaymentReturnCommand
            {
                WebhookData = webhook
            };

            await _mediator.Send(command);
            return Ok(new { success = true, message = "Đã nhận webhook." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS webhook");
            return Ok(new { success = true, message = "Đã nhận webhook." });
        }
    }
    [HttpPost("momo-ipn")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ProcessMomoIpn([FromBody] MomoIpnRequest ipnData)
    {
        try
        {
            if (ipnData == null)
            {
                _logger.LogWarning("MoMo IPN payload was null.");
                return NoContent();
            }

            _logger.LogInformation("Received MoMo IPN: OrderId={OrderId}, ResultCode={Code}, Msg={Msg}",
                ipnData.OrderId, ipnData.ResultCode, ipnData.Message);

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

    [HttpPost("zalopay-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessZaloPayCallback()
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
                return Ok(new { return_code = 2, return_message = "invalid payload" });
            }

            var gatewayService = _paymentGatewayFactory.GetService("ZALOPAY");

            var isValidSignature = gatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                _logger.LogWarning("ZaloPay Webhook signature mismatch.");
                return Ok(new { return_code = 2, return_message = "mac not equal" });
            }

            var callbackData = JsonSerializer.Deserialize<ZaloPayCallbackRequest>(jsonBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (callbackData == null || string.IsNullOrEmpty(callbackData.Data) || string.IsNullOrEmpty(callbackData.Mac))
            {
                return Ok(new { return_code = 2, return_message = "invalid payload format" });
            }

            var command = new ProcessZaloPayPaymentCommand
            {
                CallbackData = callbackData
            };

            var success = await _mediator.Send(command);

            return Ok(new
            {
                return_code = success ? 1 : 2,
                return_message = success ? "success" : "failed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ZaloPay webhook");
            return Ok(new { return_code = 2, return_message = "internal error" });
        }
    }
    /// <summary>
    /// Fallback endpoint called by the frontend after ZaloPay redirects the user back.
    /// Queries ZaloPay Order Query API directly to confirm payment and update entities
    /// when the ZaloPay callback (IPN) was not received (common in sandbox).
    /// </summary>
    [HttpGet("zalopay-verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyZaloPayPayment([FromQuery] string apptransid)
    {
        if (string.IsNullOrWhiteSpace(apptransid))
            return BadRequest(new { success = false, message = "Vui lòng cung cấp mã giao dịch apptransid." });

        try
        {
            var command = new VerifyZaloPayPaymentCommand { AppTransId = apptransid };
            var success = await _mediator.Send(command);
            return Ok(new { success, message = success ? "Xác minh và ghi nhận thanh toán thành công." : "Thanh toán chưa được xác nhận hoặc đã xử lý trước đó." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying ZaloPay payment for apptransid={AppTransId}", apptransid);
            return Ok(new { success = false, message = "Đã xảy ra lỗi trong quá trình xác minh." });
        }
    }}

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
using RESQ.Application.UseCases.Finance.Commands.VerifyPayOSPayment;
using RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;
using RESQ.Application.UseCases.Finance.Queries.GetPaymentMethodsMetadata;
using RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;
using RESQ.Domain.Enum.Finance;
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

    public DonationController(
        IMediator mediator,
        IPaymentGatewayFactory paymentGatewayFactory,
        ILogger<DonationController> logger,
        IConfiguration configuration)
    {
        _mediator = mediator;
        _paymentGatewayFactory = paymentGatewayFactory;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var result = await _mediator.Send(new GetPaymentMethodsMetadataQuery());
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
            PaymentMethodCode = dto.PaymentMethodCode
        };

        var result = await _mediator.Send(command);
        return StatusCode(StatusCodes.Status201Created, result);
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
                return Ok(new { success = false, message = "Du lieu webhook trong." });
            }

            var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.PAYOS);
            var isValidSignature = gatewayService.VerifyWebhookSignature(jsonBody);

            if (!isValidSignature)
            {
                _logger.LogWarning("PayOS webhook signature verification failed.");
                return Ok(new { success = false, message = "Chu ky webhook khong hop le." });
            }

            var webhook = JsonSerializer.Deserialize<WebhookType>(
                jsonBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (webhook == null || webhook.Data == null)
            {
                return Ok(new { success = false, message = "Du lieu webhook khong hop le." });
            }

            var processed = await _mediator.Send(new ProcessPayosPaymentReturnCommand
            {
                WebhookData = webhook
            });

            if (!processed)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { success = false, message = "Xu ly webhook PayOS that bai. Vui long thu lai." });
            }

            return Ok(new { success = true, message = "Da nhan webhook." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS webhook");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { success = false, message = "Xu ly webhook PayOS that bai. Vui long thu lai." });
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

            _logger.LogInformation(
                "Received MoMo IPN: OrderId={OrderId}, ResultCode={Code}, Msg={Msg}",
                ipnData.OrderId,
                ipnData.ResultCode,
                ipnData.Message);

            var processed = await _mediator.Send(new ProcessMomoPaymentCommand
            {
                IpnData = ipnData
            });

            if (!processed)
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return StatusCode(StatusCodes.Status500InternalServerError);
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
            return Redirect(returnUrl ?? "https://resq-sos-mientrung.vercel.app/success");
        }

        var cancelUrl = config["CancelUrl"];
        return Redirect(cancelUrl ?? "https://resq-sos-mientrung.vercel.app/fail");
    }

    /// <summary>
    /// Redirect endpoint: ZaloPay redirects the user here after payment (via embed_data.redirecturl).
    /// Backend verifies the payment via Query API, then redirects the user to the frontend success/fail page.
    /// This ensures the DB is updated even when ZaloPay's server-to-server callback does not fire.
    /// </summary>
    [HttpGet("zalopay-return")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ZaloPayReturn([FromQuery] string? apptransid, [FromQuery] int? status)
    {
        var zaloPayConfig = _configuration.GetSection("ZaloPay");
        var successUrl = zaloPayConfig["RedirectUrl"] ?? "https://resq-sos-mientrung.vercel.app/success";
        var failUrl = zaloPayConfig["CancelUrl"] ?? "https://resq-sos-mientrung.vercel.app/fail";

        // ZaloPay sends status=1 for success in the redirect query string
        if (status.HasValue && status.Value != 1)
        {
            _logger.LogInformation("ZaloPay return: user cancelled or failed (status={Status}, apptransid={AppTransId}).", status, apptransid);
            return Redirect(failUrl);
        }

        if (string.IsNullOrWhiteSpace(apptransid))
        {
            _logger.LogWarning("ZaloPay return: missing apptransid.");
            return Redirect(failUrl);
        }

        try
        {
            var command = new VerifyZaloPayPaymentCommand { AppTransId = apptransid };
            var verified = await _mediator.Send(command);

            _logger.LogInformation("ZaloPay return: verify result={Result} for apptransid={AppTransId}.", verified, apptransid);
            return Redirect(verified ? successUrl : failUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZaloPay return: error verifying apptransid={AppTransId}.", apptransid);
            return Redirect(failUrl);
        }
    }

    /// <summary>Webhook nhận kết quả thanh toán từ ZaloPay (IPN callback).</summary>
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

            var gatewayService = _paymentGatewayFactory.GetService(PaymentMethodCode.ZALOPAY);

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

    /// <summary>Xác minh kết quả thanh toán ZaloPay trực tiếp (fallback khi IPN không đến).</summary>
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
    }

    [HttpGet("payos-verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyPayOSPayment([FromQuery] string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new { success = false, message = "Vui long cung cap ma don hang orderId." });
        }

        try
        {
            var success = await _mediator.Send(new VerifyPayOSPaymentCommand { OrderId = orderId });
            return Ok(new
            {
                success,
                message = success
                    ? "Xac minh va ghi nhan thanh toan PayOS thanh cong."
                    : "Thanh toan chua duoc xac nhan hoac da duoc xu ly truoc do."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PayOS payment for orderId={OrderId}", orderId);
            return Ok(new { success = false, message = "Da xay ra loi trong qua trinh xac minh." });
        }
    }
}

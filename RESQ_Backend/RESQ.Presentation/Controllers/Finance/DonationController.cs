using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RESQ.Application.Common.Models.Finance.Momo;
using RESQ.Application.Services;
using RESQ.Application.UseCases.Finance.Commands.CreateDonation;
using RESQ.Application.UseCases.Finance.Commands.ProcessMomoPayment;
using RESQ.Application.UseCases.Finance.Commands.ProcessPayosPaymentReturn;
using RESQ.Application.UseCases.Finance.Commands.VerifyPayOSPayment;
using RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;
using RESQ.Application.UseCases.Finance.Queries.GetDonations;
using RESQ.Application.UseCases.Finance.Queries.GetPaymentMethodsMetadata;
using RESQ.Application.UseCases.Finance.Queries.GetPublicDonations;
using RESQ.Domain.Enum.Finance;
using System.Security.Cryptography;
using System.Text;
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

    [HttpGet("zalopay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> ZaloPayReturn()
    {
        var (successUrl, failUrl) = GetZaloPayFrontendUrls();
        var appTransId = GetFirstNonEmptyQueryValue(Request.Query, "apptransid", "app_trans_id");
        var statusRaw = GetFirstNonEmptyQueryValue(Request.Query, "status", "return_code");
        var checksumRaw = GetFirstNonEmptyQueryValue(Request.Query, "checksum", "mac");
        var returnParams = JsonSerializer.Serialize(
            Request.Query.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToString()));

        _logger.LogInformation(
            "ZaloPay return received. AppTransId={AppTransId}, Status={Status}, Checksum={Checksum}, ReturnParams={ReturnParams}",
            appTransId,
            statusRaw,
            MaskValue(checksumRaw),
            returnParams);

        if (string.IsNullOrWhiteSpace(appTransId))
        {
            _logger.LogWarning("ZaloPay return: missing apptransid/app_trans_id.");
            return Redirect(failUrl);
        }

        try
        {
            var checksumVerification = VerifyZaloPayRedirectChecksum(Request.Query, out var checksumFailureReason);
            if (checksumVerification == ZaloPayRedirectChecksumVerification.Invalid)
            {
                _logger.LogWarning(
                    "ZaloPay return: invalid redirect checksum for AppTransId={AppTransId}. Reason={Reason}. ReturnParams={ReturnParams}",
                    appTransId,
                    checksumFailureReason,
                    returnParams);
                return Redirect(failUrl);
            }

            if (checksumVerification == ZaloPayRedirectChecksumVerification.Missing)
            {
                _logger.LogWarning(
                    "ZaloPay return: checksum missing for AppTransId={AppTransId}. Proceeding with Query API verification. ReturnParams={ReturnParams}",
                    appTransId,
                    returnParams);
            }

            var verified = await _mediator.Send(new VerifyZaloPayPaymentCommand
            {
                AppTransId = appTransId
            });

            _logger.LogInformation(
                "ZaloPay return: verify result={Result} for AppTransId={AppTransId} (redirect status={Status}).",
                verified,
                appTransId,
                statusRaw);

            return Redirect(verified ? successUrl : failUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZaloPay return: error verifying apptransid={AppTransId}.", appTransId);
            return Redirect(failUrl);
        }
    }

    [HttpGet("zalopay-verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyZaloPayPayment([FromQuery] string apptransid)
    {
        if (string.IsNullOrWhiteSpace(apptransid))
        {
            return BadRequest(new { success = false, message = "Vui long cung cap ma giao dich apptransid." });
        }

        try
        {
            var success = await _mediator.Send(new VerifyZaloPayPaymentCommand
            {
                AppTransId = apptransid
            });

            return Ok(new
            {
                success,
                message = success
                    ? "Xac minh va ghi nhan thanh toan thanh cong."
                    : "Thanh toan chua duoc xac nhan hoac da duoc xu ly truoc do."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying ZaloPay payment for apptransid={AppTransId}", apptransid);
            return Ok(new { success = false, message = "Da xay ra loi trong qua trinh xac minh." });
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

    private (string SuccessUrl, string FailUrl) GetZaloPayFrontendUrls()
    {
        var config = _configuration.GetSection("ZaloPay");
        var successUrl = config["RedirectUrl"];
        var failUrl = config["CancelUrl"];

        if (string.IsNullOrWhiteSpace(successUrl))
        {
            throw new InvalidOperationException("Cau hinh ZaloPay thieu RedirectUrl.");
        }

        if (string.IsNullOrWhiteSpace(failUrl))
        {
            throw new InvalidOperationException("Cau hinh ZaloPay thieu CancelUrl.");
        }

        return (successUrl, failUrl);
    }

    private ZaloPayRedirectChecksumVerification VerifyZaloPayRedirectChecksum(IQueryCollection query, out string failureReason)
    {
        failureReason = string.Empty;
        var key2 = _configuration["ZaloPay:Key2"];
        if (string.IsNullOrWhiteSpace(key2))
        {
            failureReason = "missing-config-key2";
            return ZaloPayRedirectChecksumVerification.Invalid;
        }

        var checksum = GetFirstNonEmptyQueryValue(query, "checksum", "mac");
        if (string.IsNullOrWhiteSpace(checksum))
        {
            failureReason = "missing-checksum";
            return ZaloPayRedirectChecksumVerification.Missing;
        }

        var appId = GetFirstNonEmptyQueryValue(query, "appid", "app_id");
        var appTransId = GetFirstNonEmptyQueryValue(query, "apptransid", "app_trans_id");
        var pmcId = query["pmcid"].ToString();
        var bankCode = query["bankcode"].ToString();
        var amount = query["amount"].ToString();
        var discountAmount = query["discountamount"].ToString();
        var status = GetFirstNonEmptyQueryValue(query, "status", "return_code");

        if (string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(appTransId) ||
            string.IsNullOrWhiteSpace(amount) ||
            string.IsNullOrWhiteSpace(status))
        {
            failureReason = "missing-required-redirect-fields";
            return ZaloPayRedirectChecksumVerification.Invalid;
        }

        var checksumData = $"{appId}|{appTransId}|{pmcId}|{bankCode}|{amount}|{discountAmount}|{status}";
        var computedChecksum = ComputeHmacSha256(checksumData, key2);
        if (!computedChecksum.Equals(checksum, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "checksum-mismatch";
            return ZaloPayRedirectChecksumVerification.Invalid;
        }

        return ZaloPayRedirectChecksumVerification.Valid;
    }

    private static string? GetFirstNonEmptyQueryValue(IQueryCollection query, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = query[key].ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string MaskValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 8)
        {
            return "***";
        }

        return $"{value[..4]}***{value[^4..]}";
    }

    private enum ZaloPayRedirectChecksumVerification
    {
        Missing = 0,
        Valid = 1,
        Invalid = 2
    }

    private static string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}

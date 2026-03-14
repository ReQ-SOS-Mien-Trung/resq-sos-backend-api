using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services.Payments;

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentGatewayFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentGatewayService GetService(string paymentMethodCode)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodCode))
        {
             throw new ArgumentNullException(nameof(paymentMethodCode), "Mã phương thức thanh toán không được để trống.");
        }

        return paymentMethodCode.ToUpper() switch
        {
            "PAYOS" => _serviceProvider.GetRequiredService<PayOSService>(),
            "MOMO" => _serviceProvider.GetRequiredService<MomoPaymentService>(),
            "ZALOPAY" => _serviceProvider.GetRequiredService<ZaloPayService>(),
            _ => throw new ArgumentException($"Phương thức thanh toán '{paymentMethodCode}' không được hỗ trợ.")
        };
    }
}

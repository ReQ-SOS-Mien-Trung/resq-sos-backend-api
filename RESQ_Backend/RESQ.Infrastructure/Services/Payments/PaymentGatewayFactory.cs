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
             throw new ArgumentNullException(nameof(paymentMethodCode));
        }

        return paymentMethodCode.ToUpper() switch
        {
            "PAYOS" => _serviceProvider.GetRequiredService<PayOSService>(),
            "MOMO" => _serviceProvider.GetRequiredService<MomoPaymentService>(),
            _ => throw new ArgumentException($"Payment method '{paymentMethodCode}' is not supported by the system.")
        };
    }
}

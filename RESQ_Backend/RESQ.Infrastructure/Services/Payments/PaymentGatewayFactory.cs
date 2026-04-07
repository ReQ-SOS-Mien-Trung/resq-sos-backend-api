using Microsoft.Extensions.DependencyInjection;
using RESQ.Application.Services;
using RESQ.Domain.Enum.Finance;

namespace RESQ.Infrastructure.Services.Payments;

public class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly IServiceProvider _serviceProvider;

    public PaymentGatewayFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPaymentGatewayService GetService(PaymentMethodCode code)
    {
        return code switch
        {
            PaymentMethodCode.PAYOS =>
                _serviceProvider.GetRequiredService<PayOSService>(),

            PaymentMethodCode.ZALOPAY =>
                _serviceProvider.GetRequiredService<ZaloPayService>(),

            // MoMo: vẫn routing đúng để xử lý webhook/dữ liệu cũ,
            // nhưng CreateDonation đã reject trước khi đến đây.
            PaymentMethodCode.MOMO =>
                _serviceProvider.GetRequiredService<MomoPaymentService>(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(code),
                $"Phương thức thanh toán '{code}' không có service xử lý tương ứng.")
        };
    }
}

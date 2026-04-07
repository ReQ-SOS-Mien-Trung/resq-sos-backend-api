using RESQ.Domain.Enum.Finance;

namespace RESQ.Application.Services;

public interface IPaymentGatewayFactory
{
    /// <summary>
    /// Gets the payment service implementation for the given <see cref="PaymentMethodCode"/>.
    /// </summary>
    IPaymentGatewayService GetService(PaymentMethodCode code);
}

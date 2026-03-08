namespace RESQ.Application.Services;

public interface IPaymentGatewayFactory
{
    /// <summary>
    /// Gets the payment service implementation based on the database Code (e.g. "PAYOS", "MOMO").
    /// </summary>
    IPaymentGatewayService GetService(string paymentMethodCode);
}

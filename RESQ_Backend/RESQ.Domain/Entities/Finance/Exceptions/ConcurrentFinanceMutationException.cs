namespace RESQ.Domain.Entities.Finance.Exceptions;

public class ConcurrentFinanceMutationException : Exception
{
    public ConcurrentFinanceMutationException()
        : base("Quỹ hoặc chiến dịch vừa bị thay đổi bởi thao tác khác. Vui lòng thử lại.")
    {
    }

    public ConcurrentFinanceMutationException(string message)
        : base(message)
    {
    }
}

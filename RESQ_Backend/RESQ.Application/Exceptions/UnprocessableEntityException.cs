namespace RESQ.Application.Exceptions;

public class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException() : base("Không thể xử lý thực thể")
    {
    }

    public UnprocessableEntityException(string message) : base(message)
    {
    }

    public UnprocessableEntityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

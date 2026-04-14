namespace RESQ.Application.Exceptions;

public class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException() : base("Không th? x? lý th?c th?")
    {
    }

    public UnprocessableEntityException(string message) : base(message)
    {
    }

    public UnprocessableEntityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

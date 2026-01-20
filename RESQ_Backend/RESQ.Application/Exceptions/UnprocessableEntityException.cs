namespace RESQ.Application.Exceptions;

public class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException() : base("Unprocessable entity")
    {
    }

    public UnprocessableEntityException(string message) : base(message)
    {
    }

    public UnprocessableEntityException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

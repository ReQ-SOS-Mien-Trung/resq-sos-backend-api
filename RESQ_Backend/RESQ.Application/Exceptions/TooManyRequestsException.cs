namespace RESQ.Application.Exceptions;

public class TooManyRequestsException : Exception
{
    public TooManyRequestsException() : base("Too many requests")
    {
    }

    public TooManyRequestsException(string message) : base(message)
    {
    }

    public TooManyRequestsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

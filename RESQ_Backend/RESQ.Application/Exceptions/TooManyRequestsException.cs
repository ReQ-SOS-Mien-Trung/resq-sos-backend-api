namespace RESQ.Application.Exceptions;

public class TooManyRequestsException : Exception
{
    public TooManyRequestsException() : base("Quá nhi?u yêu c?u")
    {
    }

    public TooManyRequestsException(string message) : base(message)
    {
    }

    public TooManyRequestsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

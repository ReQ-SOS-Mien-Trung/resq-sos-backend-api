namespace RESQ.Application.Exceptions;

public class TooManyRequestsException : Exception
{
    public TooManyRequestsException() : base("Quá nhiều yêu cầu")
    {
    }

    public TooManyRequestsException(string message) : base(message)
    {
    }

    public TooManyRequestsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

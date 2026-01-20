namespace RESQ.Application.Exceptions;

public class MethodNotAllowedException : Exception
{
    public MethodNotAllowedException() : base("Method not allowed")
    {
    }

    public MethodNotAllowedException(string message) : base(message)
    {
    }

    public MethodNotAllowedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

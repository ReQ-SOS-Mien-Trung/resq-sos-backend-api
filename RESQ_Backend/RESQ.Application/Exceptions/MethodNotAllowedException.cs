namespace RESQ.Application.Exceptions;

public class MethodNotAllowedException : Exception
{
    public MethodNotAllowedException() : base("Phương thức không được phép")
    {
    }

    public MethodNotAllowedException(string message) : base(message)
    {
    }

    public MethodNotAllowedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

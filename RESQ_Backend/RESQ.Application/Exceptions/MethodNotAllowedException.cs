namespace RESQ.Application.Exceptions;

public class MethodNotAllowedException : Exception
{
    public MethodNotAllowedException() : base("Phuong th?c không du?c phép")
    {
    }

    public MethodNotAllowedException(string message) : base(message)
    {
    }

    public MethodNotAllowedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

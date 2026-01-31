namespace RESQ.Application.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Truy cập không được phép")
    {
    }

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

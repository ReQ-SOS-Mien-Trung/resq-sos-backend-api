namespace RESQ.Application.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Truy c?p không du?c phép")
    {
    }

    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

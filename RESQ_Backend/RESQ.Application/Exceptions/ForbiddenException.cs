namespace RESQ.Application.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException() : base("Truy c?p b? t? ch?i")
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

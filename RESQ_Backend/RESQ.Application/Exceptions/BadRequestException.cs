namespace RESQ.Application.Exceptions;

public class BadRequestException : Exception
{
    public BadRequestException() : base("Yêu c?u không h?p l?")
    {
    }

    public BadRequestException(string message) : base(message)
    {
    }

    public BadRequestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

namespace RESQ.Application.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException() : base("Truy cập bị từ chối")
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

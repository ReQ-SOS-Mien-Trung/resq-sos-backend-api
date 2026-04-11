namespace RESQ.Application.Common;

public static class ExceptionCodes
{
    public const string ExceptionDataKey = "AppErrorCode";

    public static TException WithCode<TException>(TException exception, string code)
        where TException : Exception
    {
        exception.Data[ExceptionDataKey] = code;
        return exception;
    }

    public static string? TryGet(Exception exception) => exception.Data[ExceptionDataKey] as string;
}

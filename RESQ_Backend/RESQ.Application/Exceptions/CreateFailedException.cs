namespace RESQ.Application.Exceptions
{
    public class CreateFailedException : Exception
    {
        public CreateFailedException(string input) : base($"Không thể tạo {input}")
        {
        }

        public CreateFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

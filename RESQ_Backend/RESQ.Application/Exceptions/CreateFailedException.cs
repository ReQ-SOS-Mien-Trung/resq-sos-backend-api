namespace RESQ.Application.Exceptions
{
    public class CreateFailedException : Exception
    {
        public CreateFailedException(string input) : base($"Failed to create {input}")
        {
        }

        public CreateFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

namespace RESQ.Application.UseCases.Identity.Commands.Logout
{
    public class LogoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
    }
}

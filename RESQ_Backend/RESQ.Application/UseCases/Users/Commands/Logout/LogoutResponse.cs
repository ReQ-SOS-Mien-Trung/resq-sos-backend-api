namespace RESQ.Application.UseCases.Users.Commands.Logout
{
    public class LogoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
    }
}

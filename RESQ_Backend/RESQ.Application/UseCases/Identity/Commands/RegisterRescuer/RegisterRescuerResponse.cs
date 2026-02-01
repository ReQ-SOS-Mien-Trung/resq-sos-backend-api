namespace RESQ.Application.UseCases.Identity.Commands.RegisterRescuer
{
    public class RegisterRescuerResponse
    {
        public Guid UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public int RoleId { get; set; }
        public bool IsEmailVerified { get; set; }
        public string Message { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}

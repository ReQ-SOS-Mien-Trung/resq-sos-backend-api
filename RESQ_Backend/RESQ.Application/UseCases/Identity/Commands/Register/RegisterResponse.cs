namespace RESQ.Application.UseCases.Identity.Commands.Register
{
    public class RegisterResponse
    {
        public Guid UserId { get; set; }
        public string? Phone { get; set; }
        public int RoleId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

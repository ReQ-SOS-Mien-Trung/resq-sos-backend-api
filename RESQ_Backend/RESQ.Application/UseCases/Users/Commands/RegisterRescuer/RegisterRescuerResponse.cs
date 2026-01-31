namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerResponse
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? FullName { get; set; }
        public int RoleId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

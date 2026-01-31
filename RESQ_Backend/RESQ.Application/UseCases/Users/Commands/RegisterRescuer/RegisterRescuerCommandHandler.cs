using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Users;
using RESQ.Domain.Entities;

namespace RESQ.Application.UseCases.Users.Commands.RegisterRescuer
{
    public class RegisterRescuerCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<RegisterRescuerCommandHandler> logger
    ) : IRequestHandler<RegisterRescuerCommand, RegisterRescuerResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<RegisterRescuerCommandHandler> _logger = logger;

        // Default role for rescuer
        private const int DEFAULT_RESCUER_ROLE_ID = 4;

        public async Task<RegisterRescuerResponse> Handle(RegisterRescuerCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling RegisterRescuerCommand for Username={username}", request.Username);

            // Check if username already exists
            var existingUser = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
            if (existingUser is not null)
            {
                _logger.LogWarning("Registration failed: Username already exists Username={username}", request.Username);
                throw new ConflictException("Username already registered");
            }

            // Hash password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Create new user with rescuer role
            var user = new UserModel
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Password = hashedPassword,
                RoleId = DEFAULT_RESCUER_ROLE_ID,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new CreateFailedException("Rescuer");
            }

            _logger.LogInformation("Rescuer registered successfully: UserId={userId} Username={username}", user.Id, request.Username);

            return new RegisterRescuerResponse
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                RoleId = user.RoleId ?? DEFAULT_RESCUER_ROLE_ID,
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow
            };
        }
    }
}

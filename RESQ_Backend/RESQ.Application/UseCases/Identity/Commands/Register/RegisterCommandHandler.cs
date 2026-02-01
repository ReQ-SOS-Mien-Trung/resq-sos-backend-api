using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.Register
{
    public class RegisterCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<RegisterCommandHandler> logger
    ) : IRequestHandler<RegisterCommand, RegisterResponse>
    {
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<RegisterCommandHandler> _logger = logger;

        // Default role for victim
        private const int DEFAULT_VICTIM_ROLE_ID = 5;

        public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling RegisterCommand for Phone={phone}", request.Phone);

            // Check if phone already exists
            var existingUser = await _userRepository.GetByPhoneAsync(request.Phone, cancellationToken);
            if (existingUser is not null)
            {
                _logger.LogWarning("Registration failed: Phone already exists Phone={phone}", request.Phone);
                throw new ConflictException("Số điện thoại đã được đăng ký");
            }

            // Hash password
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Create new user with default victim role
            var user = new UserModel
            {
                Id = Guid.NewGuid(),
                Phone = request.Phone,
                Password = hashedPassword,
                RoleId = DEFAULT_VICTIM_ROLE_ID,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _userRepository.CreateAsync(user, cancellationToken);
            var succeedCount = await _unitOfWork.SaveAsync();

            if (succeedCount < 1)
            {
                throw new CreateFailedException("User");
            }

            _logger.LogInformation("User registered successfully: UserId={userId} Phone={phone}", user.Id, request.Phone);

            return new RegisterResponse
            {
                UserId = user.Id,
                Phone = user.Phone,
                RoleId = user.RoleId ?? DEFAULT_VICTIM_ROLE_ID,
                CreatedAt = user.CreatedAt ?? DateTime.UtcNow
            };
        }
    }
}

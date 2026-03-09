using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.AdminCreateUser;

public class AdminCreateUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<AdminCreateUserCommandHandler> logger
) : IRequestHandler<AdminCreateUserCommand, AdminCreateUserResponse>
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AdminCreateUserCommandHandler> _logger = logger;

    public async Task<AdminCreateUserResponse> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin creating user Phone={Phone} Email={Email}", request.Phone, request.Email);

        if (!string.IsNullOrEmpty(request.Phone))
        {
            var byPhone = await _userRepository.GetByPhoneAsync(request.Phone, cancellationToken);
            if (byPhone is not null)
                throw new ConflictException("Số điện thoại đã được sử dụng");
        }

        if (!string.IsNullOrEmpty(request.Email))
        {
            var byEmail = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (byEmail is not null)
                throw new ConflictException("Email đã được sử dụng");
        }

        if (!string.IsNullOrEmpty(request.Username))
        {
            var byUsername = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
            if (byUsername is not null)
                throw new ConflictException("Username đã được sử dụng");
        }

        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new UserModel
        {
            Id = Guid.NewGuid(),
            Phone = request.Phone,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Username = request.Username,
            Password = hashedPassword,
            RoleId = request.RoleId,
            IsEmailVerified = false,
            IsOnboarded = false,
            IsBanned = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user, cancellationToken);
        var saved = await _unitOfWork.SaveAsync();
        if (saved < 1)
            throw new CreateFailedException("User");

        _logger.LogInformation("Admin created user UserId={UserId}", user.Id);

        return new AdminCreateUserResponse
        {
            Id = user.Id,
            RoleId = user.RoleId,
            Phone = user.Phone,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            CreatedAt = user.CreatedAt
        };
    }
}

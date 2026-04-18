using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteRelativeProfile
{
    public class DeleteRelativeProfileCommandHandler : IRequestHandler<DeleteRelativeProfileCommand, Unit>
    {
        private readonly IRelativeProfileRepository _repository;

        public DeleteRelativeProfileCommandHandler(IRelativeProfileRepository repository)
        {
            _repository = repository;
        }

        public async Task<Unit> Handle(DeleteRelativeProfileCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(request.ProfileId, request.UserId, cancellationToken);
            if (existing == null)
                throw new NotFoundException($"Relative profile {request.ProfileId} not found.");

            await _repository.DeleteAsync(request.ProfileId, request.UserId, cancellationToken);
            return Unit.Value;
        }
    }
}

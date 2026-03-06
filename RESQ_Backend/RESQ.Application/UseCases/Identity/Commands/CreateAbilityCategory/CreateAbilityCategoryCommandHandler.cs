using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Identity;
using RESQ.Domain.Entities.Identity;

namespace RESQ.Application.UseCases.Identity.Commands.CreateAbilityCategory;

public class CreateAbilityCategoryCommandHandler(
    IAbilityCategoryRepository repository)
    : IRequestHandler<CreateAbilityCategoryCommand, CreateAbilityCategoryResponse>
{
    private readonly IAbilityCategoryRepository _repository = repository;

    public async Task<CreateAbilityCategoryResponse> Handle(CreateAbilityCategoryCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByCodeAsync(request.Code, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Danh mục ability với code '{request.Code}' đã tồn tại.");

        var model = AbilityCategoryModel.Create(request.Code, request.Description);
        var id = await _repository.CreateAsync(model, cancellationToken);

        if (id == 0)
            throw new CreateFailedException("danh mục ability");

        var created = await _repository.GetByIdAsync(id, cancellationToken);

        return new CreateAbilityCategoryResponse
        {
            Id = created!.Id,
            Code = created.Code,
            Description = created.Description
        };
    }
}

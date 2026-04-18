using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.CreateAbilityCategory;

public record CreateAbilityCategoryCommand(string Code, string? Description) : IRequest<CreateAbilityCategoryResponse>;

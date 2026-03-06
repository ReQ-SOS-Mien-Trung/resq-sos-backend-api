using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.UpdateAbilityCategory;

public record UpdateAbilityCategoryCommand(int Id, string Code, string? Description) : IRequest;

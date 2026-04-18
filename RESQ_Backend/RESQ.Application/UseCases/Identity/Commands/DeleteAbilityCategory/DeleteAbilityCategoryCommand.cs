using MediatR;

namespace RESQ.Application.UseCases.Identity.Commands.DeleteAbilityCategory;

public record DeleteAbilityCategoryCommand(int Id) : IRequest;

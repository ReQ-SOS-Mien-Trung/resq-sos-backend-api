using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateItemCategory;

public record CreateItemCategoryCommand(
    ItemCategoryCode Code,
    string Name,
    string Description
) : IRequest<CreateItemCategoryResponse>;

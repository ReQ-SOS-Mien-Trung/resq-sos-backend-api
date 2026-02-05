using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.DeleteItemCategory;

public record DeleteItemCategoryCommand(int Id) : IRequest;
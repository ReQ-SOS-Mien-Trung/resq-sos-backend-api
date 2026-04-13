namespace RESQ.Application.UseCases.Logistics.Queries.GetClosureTargetDepots;

using MediatR;
using System.Collections.Generic;

public class GetClosureTargetDepotsQuery : IRequest<List<TargetDepotKeyValueDto>>
{
}

public class TargetDepotKeyValueDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointUnavailableImpact;

public record GetAssemblyPointUnavailableImpactQuery(int AssemblyPointId) : IRequest<AssemblyPointUnavailableImpactResponse>;

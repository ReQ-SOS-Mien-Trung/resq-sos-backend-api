using MediatR;
using RESQ.Application.Common.Models;

namespace RESQ.Application.UseCases.Finance.Queries.ViewCampaignMetadata;

public record ViewCampaignMetadataQuery : IRequest<List<MetadataDto>>;

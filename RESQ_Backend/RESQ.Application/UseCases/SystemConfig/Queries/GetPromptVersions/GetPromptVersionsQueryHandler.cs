using MediatR;
using Microsoft.Extensions.Logging;
using RESQ.Application.Common;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.SystemConfig.Queries.GetPromptVersions;

public class GetPromptVersionsQueryHandler(
    IPromptRepository promptRepository,
    ILogger<GetPromptVersionsQueryHandler> logger) : IRequestHandler<GetPromptVersionsQuery, GetPromptVersionsResponse>
{
    private readonly IPromptRepository _promptRepository = promptRepository;
    private readonly ILogger<GetPromptVersionsQueryHandler> _logger = logger;

    public async Task<GetPromptVersionsResponse> Handle(GetPromptVersionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting prompt versions by source Id={Id}", request.Id);

        var source = await _promptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (source == null)
        {
            throw new NotFoundException($"Không tìm thấy prompt với Id={request.Id}");
        }

        var versions = await _promptRepository.GetVersionsByTypeAsync(source.PromptType, cancellationToken);

        var orderedVersions = versions
            .OrderBy(prompt => GetStatusOrder(prompt))
            .ThenByDescending(prompt => prompt.UpdatedAt ?? prompt.CreatedAt)
            .ThenByDescending(prompt => prompt.Id)
            .ToList();

        return new GetPromptVersionsResponse
        {
            SourcePromptId = source.Id,
            PromptType = source.PromptType,
            Items = orderedVersions.Select(p => new PromptVersionSummaryDto
            {
                Id = p.Id,
                Status = PromptLifecycleStatusResolver.DetermineStatus(p),
                Name = p.Name,
                PromptType = p.PromptType,
                Version = p.Version,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList()
        };
    }

    private static int GetStatusOrder(RESQ.Domain.Entities.System.PromptModel prompt)
    {
        return PromptLifecycleStatusResolver.DetermineStatus(prompt) switch
        {
            "Active" => 0,
            "Draft" => 1,
            _ => 2
        };
    }
}

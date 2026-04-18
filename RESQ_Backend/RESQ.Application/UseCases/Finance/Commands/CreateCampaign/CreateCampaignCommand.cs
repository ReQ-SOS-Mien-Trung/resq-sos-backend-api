using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.CreateCampaign;

public record CreateCampaignCommand(
    string Name, 
    string Region, 
    DateOnly CampaignStartDate, 
    DateOnly CampaignEndDate, 
    decimal TargetAmount, 
    Guid CreatedBy
) : IRequest<int>;

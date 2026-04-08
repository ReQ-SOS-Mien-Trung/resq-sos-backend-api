using FluentValidation;
using RESQ.Application.Common;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.Emergency.Commands.CreateSosRequest;

public class CreateSosRequestCommandValidator : AbstractValidator<CreateSosRequestCommand>
{
    public CreateSosRequestCommandValidator(
        IServiceZoneRepository serviceZoneRepository,
        ISosPriorityRuleConfigRepository sosPriorityRuleConfigRepository)
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RawMessage).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Location.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Location.Longitude).InclusiveBetween(-180, 180);

        RuleFor(x => x.Location)
            .MustAsync(async (loc, cancellationToken) =>
                await serviceZoneRepository.IsLocationInServiceZoneAsync(
                    loc.Latitude, loc.Longitude, cancellationToken))
            .WithMessage("Vị trí của bạn nằm ngoài vùng phục vụ đã được cấu hình.");

        RuleFor(x => x.StructuredData)
            .CustomAsync(async (structuredData, context, cancellationToken) =>
            {
                var configModel = await sosPriorityRuleConfigRepository.GetAsync(cancellationToken);
                var config = SosPriorityRuleConfigSupport.FromModel(configModel);
                foreach (var error in SosStructuredDataValidationHelper.Validate(structuredData, config))
                {
                    context.AddFailure(nameof(CreateSosRequestCommand.StructuredData), error);
                }
            });
    }
}
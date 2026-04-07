using FluentValidation;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.Emergency.Commands.UpdateSosRequestVictim;

public class UpdateSosRequestVictimCommandValidator : AbstractValidator<UpdateSosRequestVictimCommand>
{
    public UpdateSosRequestVictimCommandValidator(IServiceZoneRepository serviceZoneRepository)
    {
        RuleFor(x => x.SosRequestId).GreaterThan(0);
        RuleFor(x => x.RequestedByUserId).NotEmpty();
        RuleFor(x => x.RawMessage).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Location.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Location.Longitude).InclusiveBetween(-180, 180);

        RuleFor(x => x.Location)
            .MustAsync(async (loc, cancellationToken) =>
                await serviceZoneRepository.IsLocationInServiceZoneAsync(
                    loc.Latitude, loc.Longitude, cancellationToken))
            .WithMessage("Vị trí của bạn nằm ngoài vùng phục vụ đã được cấu hình.");
    }
}
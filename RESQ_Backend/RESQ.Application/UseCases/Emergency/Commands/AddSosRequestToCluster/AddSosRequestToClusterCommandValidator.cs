using FluentValidation;
using RESQ.Application.UseCases.Emergency.Shared;

namespace RESQ.Application.UseCases.Emergency.Commands.AddSosRequestToCluster;

public class AddSosRequestToClusterCommandValidator : AbstractValidator<AddSosRequestToClusterCommand>
{
    public AddSosRequestToClusterCommandValidator()
    {
        RuleFor(x => x.ClusterId)
            .GreaterThan(0)
            .WithMessage("ClusterId không hợp lệ.");

        RuleFor(x => x.SosRequestIds)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Phải chọn ít nhất một SOS request để thêm vào cluster.")
            .Must(ids => ids.All(id => id > 0))
            .WithMessage("Danh sách SOS request có ID không hợp lệ.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Danh sách SOS request không được có ID trùng lặp.")
            .Must(ids => ids.Distinct().Count() <= SosClusterCapacityLimits.MaxSosRequests)
            .WithMessage($"Một cluster chỉ có thể chứa tối đa {SosClusterCapacityLimits.MaxSosRequests} SOS request.");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty()
            .WithMessage("RequestedByUserId không hợp lệ.");
    }
}

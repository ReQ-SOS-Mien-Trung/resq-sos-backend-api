using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Personnel;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Commands.CheckOutAtAssemblyPoint
{
    public class CheckOutAtAssemblyPointCommandHandler : IRequestHandler<CheckOutAtAssemblyPointCommand>
    {
        private readonly IAssemblyEventRepository _assemblyEventRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CheckOutAtAssemblyPointCommandHandler(
            IAssemblyEventRepository assemblyEventRepository,
            IUnitOfWork unitOfWork)
        {
            _assemblyEventRepository = assemblyEventRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(CheckOutAtAssemblyPointCommand request, CancellationToken cancellationToken)
        {
            var evt = await _assemblyEventRepository.GetEventByIdAsync(request.EventId, cancellationToken)
                ?? throw new NotFoundException($"Sự kiện tập trung không tồn tại: {request.EventId}");

            if (evt.Status != AssemblyEventStatus.Gathering.ToString())
                throw new BadRequestException($"Sự kiện không ở trạng thái đang tập hợp. Trạng thái hiện tại: {evt.Status}");

            var success = await _assemblyEventRepository.CheckOutAsync(request.EventId, request.RescuerId, cancellationToken);
            if (!success)
                throw new BadRequestException("Bạn chưa check-in hoặc không nằm trong danh sách tham gia.");

            await _unitOfWork.SaveAsync();
        }
    }
}

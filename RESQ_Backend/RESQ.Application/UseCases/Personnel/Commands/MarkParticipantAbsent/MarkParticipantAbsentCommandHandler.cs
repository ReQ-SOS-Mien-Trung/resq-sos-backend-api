using MediatR;
using RESQ.Application.Exceptions;

namespace RESQ.Application.UseCases.Personnel.Commands.MarkParticipantAbsent;

public class MarkParticipantAbsentCommandHandler : IRequestHandler<MarkParticipantAbsentCommand>
{
    public Task Handle(MarkParticipantAbsentCommand request, CancellationToken cancellationToken)
    {
        throw new BadRequestException(
            "Chức năng đánh dấu vắng mặt không còn được sử dụng. Vui lòng dùng luồng xóa thành viên khỏi đội nếu cần điều chỉnh nhân sự.");
    }
}

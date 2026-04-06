using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots
{
    public class GetAllDepotsQuery : IRequest<GetAllDepotsResponse>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        /// <summary>Lọc theo một hoặc nhiều trạng thái kho. Bỏ trống = lấy tất cả.</summary>
        public List<DepotStatus>? Statuses { get; set; }

        /// <summary>Tìm kiếm theo tên kho hoặc tên người quản kho hiện tại (LastName / FirstName).</summary>
        public string? Search { get; set; }
    }
}

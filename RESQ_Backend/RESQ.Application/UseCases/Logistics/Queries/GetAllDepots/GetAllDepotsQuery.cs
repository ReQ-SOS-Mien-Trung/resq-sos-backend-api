using MediatR;
using RESQ.Domain.Enum.Logistics;

namespace RESQ.Application.UseCases.Logistics.Queries.GetAllDepots
{
    public class GetAllDepotsQuery : IRequest<GetAllDepotsResponse>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        /// <summary>L?c theo m?t ho?c nhi?u tr?ng thái kho. B? tr?ng = l?y t?t c?.</summary>
        public List<DepotStatus>? Statuses { get; set; }

        /// <summary>Tìm ki?m theo tên kho ho?c tên ngu?i qu?n kho hi?n t?i (LastName / FirstName).</summary>
        public string? Search { get; set; }
    }
}

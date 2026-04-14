using RESQ.Application.Common.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RESQ.Application.Services
{
    public class ManagedDepotDto
    {
        public int DepotId { get; set; }
        public string DepotName { get; set; } = string.Empty;
    }

    public interface IManagerDepotAccessService
    {
        /// <summary>
        /// Lấy danh sách các kho mà user (quản kho) đang có quyền quản lý.
        /// </summary>
        Task<List<ManagedDepotDto>> GetManagedDepotsAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Kiểm tra quyền truy cập kho theo DepotId được yêu cầu. 
        /// Nếu user không quản lý kho đó, ném lỗi ForbiddenException.
        /// Trả về DepotId hợp lệ. Nếu requestedDepotId = null và user chỉ quản lý 1 kho, trả về kho đó.
        /// </summary>
        Task<int> ResolveAccessibleDepotIdAsync(Guid userId, int? requestedDepotId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Đảm bảo user có quyền quản lý kho này. Nếu không, ném lỗi ForbiddenException.
        /// </summary>
        Task EnsureDepotAccessAsync(Guid userId, int depotId, CancellationToken cancellationToken = default);
    }
}

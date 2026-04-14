using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Services;

namespace RESQ.Infrastructure.Services.Logistics
{
    public class ManagerDepotAccessService(
        IDepotInventoryRepository depotInventoryRepository,
        IDepotRepository depotRepository) : IManagerDepotAccessService
    {
        private readonly IDepotInventoryRepository _depotInventoryRepository = depotInventoryRepository;
        private readonly IDepotRepository _depotRepository = depotRepository;

        public async Task<List<ManagedDepotDto>> GetManagedDepotsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var depotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(userId, cancellationToken);
            
            var managedDepots = new List<ManagedDepotDto>();
            foreach (var id in depotIds)
            {
                var depot = await _depotRepository.GetByIdAsync(id, cancellationToken);
                if (depot != null)
                {
                    managedDepots.Add(new ManagedDepotDto
                    {
                        DepotId = depot.Id,
                        DepotName = depot.Name ?? string.Empty,
                        Status = depot.Status.ToString(),
                        Address = depot.Address ?? string.Empty,
                        ImageUrl = depot.ImageUrl
                    });
                }
            }

            return managedDepots;
        }

        public async Task<int?> ResolveAccessibleDepotIdAsync(Guid userId, int? requestedDepotId, CancellationToken cancellationToken = default)
        {
            var depotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(userId, cancellationToken);

            if (depotIds == null || depotIds.Count == 0)
            {
                return null;
            }

            if (requestedDepotId.HasValue)
            {
                if (!depotIds.Contains(requestedDepotId.Value))
                {
                    throw new ForbiddenException($"Người dùng không có quyền (hoặc chưa được phân công) thao tác với kho ID = {requestedDepotId.Value}.");
                }
                return requestedDepotId.Value;
            }

            // Nếu không cung cấp DepotId cụ thể nhưng quản lý nhiều kho -> Bắt buộc phải chọn 1 kho
            if (depotIds.Count > 1)
            {
                throw new BadRequestException("Bạn đang quản lý nhiều kho. Vui lòng chỉ định `DepotId` cụ thể trong yêu cầu.");
            }

            // Nếu chỉ quản lý đúng 1 kho, trả về luôn kho đó
            return depotIds.First();
        }

        public async Task EnsureDepotAccessAsync(Guid userId, int depotId, CancellationToken cancellationToken = default)
        {
            var depotIds = await _depotInventoryRepository.GetActiveDepotIdsByManagerAsync(userId, cancellationToken);
            
            if (depotIds == null || !depotIds.Contains(depotId))
            {
                throw new ForbiddenException($"Người dùng không được cấp quyền quản lý kho ID = {depotId}.");
            }
        }
    }
}

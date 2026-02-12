using RESQ.Application.Common.Models;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.Identity;
using RESQ.Application.UseCases.Identity.Queries.GetRescuerApplications;
using RESQ.Domain.Entities.Identity;
using RESQ.Infrastructure.Entities.Identity;
using RESQ.Infrastructure.Mappers.Identity;

namespace RESQ.Infrastructure.Persistence.Identity
{
    public class RescuerApplicationRepository(IUnitOfWork unitOfWork) : IRescuerApplicationRepository
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        public async Task<RescuerApplicationModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<RescuerApplication>()
                .GetByPropertyAsync(
                    x => x.Id == id,
                    includeProperties: "RescuerApplicationDocuments"
                );
            return entity is null ? null : RescuerApplicationMapper.ToModel(entity);
        }

        public async Task<RescuerApplicationModel?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<RescuerApplication>()
                .GetByPropertyAsync(
                    x => x.UserId == userId,
                    includeProperties: "RescuerApplicationDocuments"
                );
            return entity is null ? null : RescuerApplicationMapper.ToModel(entity);
        }

        public async Task<RescuerApplicationModel?> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<RescuerApplication>()
                .GetByPropertyAsync(
                    x => x.UserId == userId && x.Status == "Pending",
                    includeProperties: "RescuerApplicationDocuments"
                );
            return entity is null ? null : RescuerApplicationMapper.ToModel(entity);
        }

        public async Task<RescuerApplicationDto?> GetLatestByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var entities = await _unitOfWork.GetRepository<RescuerApplication>()
                .GetAllByPropertyAsync(
                    x => x.UserId == userId,
                    includeProperties: "RescuerApplicationDocuments,User"
                );

            var entity = entities.OrderByDescending(x => x.SubmittedAt).FirstOrDefault();
            return entity is null ? null : MapToDto(entity);
        }

        public async Task<PagedResult<RescuerApplicationDto>> GetPagedAsync(int pageNumber, int pageSize, string? status = null, CancellationToken cancellationToken = default)
        {
            var pagedResult = await _unitOfWork.GetRepository<RescuerApplication>()
                .GetPagedAsync(
                    pageNumber,
                    pageSize,
                    filter: status is null ? null : x => x.Status == status,
                    orderBy: q => q.OrderByDescending(x => x.SubmittedAt),
                    includeProperties: "RescuerApplicationDocuments,User"
                );

            var dtos = pagedResult.Items.Select(MapToDto).ToList();

            return new PagedResult<RescuerApplicationDto>(
                dtos,
                pagedResult.TotalCount,
                pagedResult.PageNumber,
                pagedResult.PageSize
            );
        }

        public async Task<int> CreateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default)
        {
            var entity = RescuerApplicationMapper.ToEntity(application);
            await _unitOfWork.GetRepository<RescuerApplication>().AddAsync(entity);
            await _unitOfWork.SaveAsync();
            return entity.Id;
        }

        public async Task UpdateAsync(RescuerApplicationModel application, CancellationToken cancellationToken = default)
        {
            var entity = await _unitOfWork.GetRepository<RescuerApplication>()
                .GetByPropertyAsync(x => x.Id == application.Id);
            
            if (entity is not null)
            {
                entity.Status = application.Status;
                entity.ReviewedAt = application.ReviewedAt;
                entity.ReviewedBy = application.ReviewedBy;
                entity.AdminNote = application.AdminNote;

                await _unitOfWork.GetRepository<RescuerApplication>().UpdateAsync(entity);
            }
        }

        public async Task AddDocumentsAsync(int applicationId, List<RescuerApplicationDocumentModel> documents, CancellationToken cancellationToken = default)
        {
            var entities = documents.Select(d =>
            {
                d.ApplicationId = applicationId;
                return RescuerApplicationMapper.ToDocumentEntity(d);
            }).ToList();

            await _unitOfWork.GetRepository<RescuerApplicationDocument>().AddRangeAsync(entities);
        }

        private static RescuerApplicationDto MapToDto(RescuerApplication entity)
        {
            return new RescuerApplicationDto
            {
                Id = entity.Id,
                UserId = entity.UserId ?? Guid.Empty,
                Status = entity.Status,
                SubmittedAt = entity.SubmittedAt,
                ReviewedAt = entity.ReviewedAt,
                ReviewedBy = entity.ReviewedBy,
                AdminNote = entity.AdminNote,
                FullName = entity.User?.FullName,
                Email = entity.User?.Email,
                Phone = entity.User?.Phone,
                RescuerType = entity.User?.RescuerType,
                Address = entity.User?.Address,
                Ward = entity.User?.Ward,
                City = entity.User?.City,
                Documents = entity.RescuerApplicationDocuments.Select(d => new RescuerApplicationDocumentDto
                {
                    Id = d.Id,
                    FileUrl = d.FileUrl,
                    FileType = d.FileType,
                    UploadedAt = d.UploadedAt
                }).ToList()
            };
        }
    }
}

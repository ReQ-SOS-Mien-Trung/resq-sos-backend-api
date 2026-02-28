using RESQ.Domain.Entities.Identity;
using RESQ.Domain.Enum.Identity;
using RESQ.Infrastructure.Entities.Identity;


namespace RESQ.Infrastructure.Mappers.Identity
{
    public static class RescuerApplicationMapper
    {
        public static RescuerApplication ToEntity(RescuerApplicationModel model)
        {
            return new RescuerApplication
            {
                Id = model.Id,
                UserId = model.UserId,
                Status = model.Status.ToString(),
                SubmittedAt = model.SubmittedAt,
                ReviewedAt = model.ReviewedAt,
                ReviewedBy = model.ReviewedBy,
                AdminNote = model.AdminNote
            };
        }

        public static RescuerApplicationModel ToModel(RescuerApplication entity)
        {
            var model = new RescuerApplicationModel
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Status = Enum.TryParse<RescuerApplicationStatus>(entity.Status, out var status)
                    ? status
                    : RescuerApplicationStatus.Pending,
                SubmittedAt = entity.SubmittedAt,
                ReviewedAt = entity.ReviewedAt,
                ReviewedBy = entity.ReviewedBy,
                AdminNote = entity.AdminNote,
                Documents = entity.RescuerApplicationDocuments
                    .Select(ToDocumentModel)
                    .ToList()
            };
            return model;
        }

        public static RescuerApplicationDocument ToDocumentEntity(RescuerApplicationDocumentModel model)
        {
            return new RescuerApplicationDocument
            {
                Id = model.Id,
                ApplicationId = model.ApplicationId,
                FileUrl = model.FileUrl,
                FileTypeId = model.FileTypeId,
                UploadedAt = model.UploadedAt
            };
        }

        public static RescuerApplicationDocumentModel ToDocumentModel(RescuerApplicationDocument entity)
        {
            return new RescuerApplicationDocumentModel
            {
                Id = entity.Id,
                ApplicationId = entity.ApplicationId,
                FileUrl = entity.FileUrl,
                FileTypeId = entity.FileTypeId,
                FileTypeCode = entity.FileType?.Code,
                FileTypeName = entity.FileType?.Name,
                UploadedAt = entity.UploadedAt
            };
        }
    }
}

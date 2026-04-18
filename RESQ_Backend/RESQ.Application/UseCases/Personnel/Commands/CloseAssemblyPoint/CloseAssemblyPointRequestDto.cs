using System.ComponentModel.DataAnnotations;

namespace RESQ.Application.UseCases.Personnel.Commands.CloseAssemblyPoint;

public class CloseAssemblyPointRequestDto
{
    /// <summary>Lý do đóng điểm tập kết (bắt buộc).</summary>
    [Required]
    public string Reason { get; set; } = string.Empty;
}

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

public class SetAssemblyPointUnavailableRequestDto
{
    /// <summary>Lý do đánh dấu không khả dụng (tùy chọn).</summary>
    public string? Reason { get; set; }
}

namespace RESQ.Infrastructure.Services;

internal sealed class GoongLeg
{
    public GoongTextValue? Distance { get; set; }
    public GoongTextValue? Duration { get; set; }
    public string? StartAddress { get; set; }
    public string? EndAddress { get; set; }
    public List<GoongStep>? Steps { get; set; }
}

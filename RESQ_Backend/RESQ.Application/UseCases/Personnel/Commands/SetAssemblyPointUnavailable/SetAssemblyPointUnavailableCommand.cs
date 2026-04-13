using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.SetAssemblyPointUnavailable;

/// <summary>
/// Admin Ä‘Æ°a Ä‘iá»ƒm táº­p káº¿t vÃ o báº£o trÃ¬: Active â†’ UnderMaintenance hoáº·c Overloaded â†’ UnderMaintenance.
/// </summary>
public record SetAssemblyPointUnavailableCommand(int Id) : IRequest<SetAssemblyPointUnavailableResponse>;


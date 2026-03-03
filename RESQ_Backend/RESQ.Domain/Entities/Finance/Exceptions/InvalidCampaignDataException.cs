using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

/// <summary>
/// Thrown when input data (Name, Region) fails domain validation.
/// </summary>
public class InvalidCampaignDataException : DomainException
{
    public InvalidCampaignDataException(string field) 
        : base($"{field} không được để trống.") { }
}
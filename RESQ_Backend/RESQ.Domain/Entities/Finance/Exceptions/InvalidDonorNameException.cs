using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidDonorNameException : DomainException
{
    public InvalidDonorNameException() : base("Tên người ủng hộ không được để trống.")
    {
    }
}
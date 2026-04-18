using RESQ.Domain.Entities.Exceptions;

namespace RESQ.Domain.Entities.Finance.Exceptions;

public class InvalidDonorEmailException : DomainException
{
    public InvalidDonorEmailException() : base("Email người ủng hộ không hợp lệ.")
    {
    }
}

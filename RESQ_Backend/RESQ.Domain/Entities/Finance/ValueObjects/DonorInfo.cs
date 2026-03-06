using RESQ.Domain.Entities.Finance.Exceptions;
using System.Net.Mail;

namespace RESQ.Domain.Entities.Finance.ValueObjects;

public record DonorInfo
{
    public string Name { get; init; }
    public string Email { get; init; }

    public DonorInfo(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDonorNameException();
        }

        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            // FIXED: Throw specific email exception instead of Name exception
            throw new InvalidDonorEmailException(); 
        }

        Name = name.Trim();
        Email = email.Trim();
    }

    public static DonorInfo Create(string name, string email)
    {
        return new DonorInfo(name, email);
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}

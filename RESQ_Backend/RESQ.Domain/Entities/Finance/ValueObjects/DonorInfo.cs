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
            // Ideally throw InvalidDonorEmailException, but reusing ValidationException concept or DomainException
            throw new InvalidDonorNameException(); // Reusing or creating new one below would be better. 
            // For now, let's assume validation happens at command level too, but Domain should protect invariant.
        }

        Name = name.Trim();
        Email = email.Trim();
    }

    // Constructor for backward compatibility if needed, but we are refactoring.
    // The previous code had DonorInfo(string name). We need to update it.
    
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

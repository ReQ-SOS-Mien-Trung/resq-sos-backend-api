using System;

namespace RESQ.Domain.Entities.Users.Exceptions
{
    public class InvalidCredentialsException : Exception
    {
        public InvalidCredentialsException()
            : base("Invalid username or password.") { }
        public InvalidCredentialsException(string? message) : base(message) { }
    }
}

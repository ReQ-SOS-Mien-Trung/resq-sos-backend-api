using System;

namespace RESQ.Domain.Exceptions
{
    public class InvalidCredentialsException : Exception
    {
        public InvalidCredentialsException()
            : base("Invalid username or password.") { }
        public InvalidCredentialsException(string? message) : base(message) { }
    }
}

using System;

namespace RESQ.Domain.Exceptions
{
    public class UserAlreadyExistsException : Exception
    {
        public UserAlreadyExistsException()
            : base("User with the given username already exists.") { }
        public UserAlreadyExistsException(string? message) : base(message) { }
    }
}

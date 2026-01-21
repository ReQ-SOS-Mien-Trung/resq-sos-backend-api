using System;

namespace RESQ.Domain.Entities.Users.Exceptions
{
    public class UserAlreadyExistsException : Exception
    {
        public UserAlreadyExistsException()
            : base("User with the given username already exists.") { }
        public UserAlreadyExistsException(string? message) : base(message) { }
    }
}

using BatalhaNaval.Application.Interfaces;
using BCrypt.Net;

namespace BatalhaNaval.Application.Services;

public class PasswordService : IPasswordService
{
    public string HashPassword(string plainTextPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainTextPassword);
    }

    public bool VerifyPassword(string plainTextPassword, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(plainTextPassword, passwordHash);
    }
}
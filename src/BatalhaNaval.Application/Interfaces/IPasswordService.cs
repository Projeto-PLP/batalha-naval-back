namespace BatalhaNaval.Application.Interfaces;

public interface IPasswordService
{
    string HashPassword(string plainTextPassword);
    bool VerifyPassword(string plainTextPassword, string passwordHash);
}
using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}
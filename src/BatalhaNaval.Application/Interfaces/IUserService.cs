using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Application.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> RegisterUserAsync(CreateUserDto dto);
    Task<User> GetByIdAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}
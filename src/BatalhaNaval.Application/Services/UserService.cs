using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Interfaces;

namespace BatalhaNaval.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;

    public UserService(IUserRepository userRepository, IPasswordService passwordService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
    }

    public async Task<UserResponseDto> RegisterUserAsync(CreateUserDto dto)
    {
        if (await _userRepository.ExistsByUsernameAsync(dto.Username))
            throw new InvalidOperationException("Nome de usuário já está em uso.");

        var passwordHash = _passwordService.HashPassword(dto.Password);

        var newUser = new User
        {
            Username = dto.Username,
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            Profile = new PlayerProfile
            {
                RankPoints = 0,
                Wins = 0,
                Losses = 0
                // UpdatedAt = DateTime.UtcNow
            }
        };

        var createdUser = await _userRepository.AddAsync(newUser);

        return new UserResponseDto(createdUser.Id, createdUser.Username, createdUser.CreatedAt);
    }
}
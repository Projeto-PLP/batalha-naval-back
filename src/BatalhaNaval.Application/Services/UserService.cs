using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Interfaces;

namespace BatalhaNaval.Application.Services;

public class UserService : IUserService
{
    private readonly IPasswordService _passwordService;
    private readonly IUserRepository _userRepository;

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

    public async Task<User> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return null;

        return user;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _userRepository.ExistsAsync(id);
    }

    public async Task<List<RankingEntryDto>> GetRankingAsync()
    {
        //o repositório deve retornar os perfis ordenados por RankPoints DESC
        var profiles = await _userRepository.GetTopPlayersAsync(100);

        return profiles.Select(p => new RankingEntryDto(
            p.UserId,
            p.User.Username,
            p.RankPoints,
            p.Wins,
            CalculateRank(p.RankPoints)
        )).ToList();
    }

    private string CalculateRank(int points)
    {
        return points switch
        {
            >= 5000 => "S",
            >= 3000 => "A",
            >= 1500 => "B",
            _ => "C"
        };
    }
}
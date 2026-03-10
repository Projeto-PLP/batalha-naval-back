using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.Interfaces;

namespace BatalhaNaval.Application.Services;

public class UserService : IUserService
{
    private readonly IPasswordService _passwordService;
    private readonly IUserRepository _userRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IMedalRepository _medalRepository;

    public UserService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        IMatchRepository matchRepository,
        IMedalRepository medalRepository)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _matchRepository = matchRepository;
        _medalRepository = medalRepository;
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
    
    public async Task<UserProfileDTO> GetUserProfileAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return null;

        var userMedals = await _medalRepository.GetUserMedalsAsync(userId);

        return new UserProfileDTO
        {
            Id = user.Id,
            Username = user.Username,
            RankPoints = user.Profile.RankPoints,
            Wins = user.Profile.Wins,
            Losses = user.Profile.Losses,
            EarnedMedalCodes = userMedals.Select(um => um.Medal.Code).ToList()
        };
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

    public async Task<List<MatchHistoryResponseDto>> GetMatchHistoryAsync(Guid playerId)
    {
        var matches = await _matchRepository.GetPlayerMatchHistoryAsync(playerId);

        var opponentIds = matches
            .Where(m => m.Player2Id.HasValue)
            .Select(m => m.Player1Id == playerId ? m.Player2Id!.Value : m.Player1Id)
            .Distinct()
            .ToList();

        // Carrega os nomes dos oponentes humanos de uma só vez
        var opponentNames = new Dictionary<Guid, string>();
        foreach (var opponentId in opponentIds)
        {
            var opponent = await _userRepository.GetByIdAsync(opponentId);
            if (opponent != null)
                opponentNames[opponentId] = opponent.Username;
        }

        return matches.Select(m => MapToHistoryDto(m, playerId, opponentNames)).ToList();
    }

    private static MatchHistoryResponseDto MapToHistoryDto(
        Match match,
        Guid playerId,
        Dictionary<Guid, string> opponentNames)
    {
        // 1. Resolve o nome do oponente (Tratando Campanha e IA)
        string? opponentName;
        if (!match.Player2Id.HasValue)
        {
            if (match.IsCampaignMatch)
            {
                opponentName = match.CampaignStage switch
                {
                    CampaignStage.Stage1Basic => "Campanha - Estágio 1",
                    CampaignStage.Stage2Intermediate => "Campanha - Estágio 2",
                    CampaignStage.Stage3Advanced => "Campanha - Estágio 3",
                    _ => "Modo Campanha"
                };
            }
            else
            {
                opponentName = match.AiDifficulty switch
                {
                    Difficulty.Basic => "IA - Básico",
                    Difficulty.Intermediate => "IA - Intermediário",
                    Difficulty.Advanced => "IA - Avançado",
                    _ => "Máquina (IA)"
                };
            }
        }
        else
        {
            var opponentId = match.Player1Id == playerId ? match.Player2Id!.Value : match.Player1Id;
            opponentNames.TryGetValue(opponentId, out opponentName);
            opponentName ??= "Jogador Desconhecido"; // Fallback de segurança
        }

        // 2. Resolve o resultado
        string result;
        if (match.WinnerId == null)
            result = "Cancelada / Empate";
        else if (match.WinnerId == playerId)
            result = "Vitória";
        else
            result = "Derrota";

        // 3. Resolve o modo de jogo
        string gameMode;
        if (match.IsCampaignMatch)
            gameMode = "História";
        else
            gameMode = match.Mode switch
            {
                GameMode.Classic => "Clássico",
                GameMode.Dynamic => "Dinâmico",
                _ => match.Mode.ToString()
            };

        // 4. Data e duração com tratamento de nulos seguro
        var playedAt = match.FinishedAt ?? match.StartedAt;
        
        TimeSpan? duration = null;
        if (match.FinishedAt.HasValue && match.StartedAt != default)
        {
            duration = match.FinishedAt.Value - match.StartedAt;
        }

        return new MatchHistoryResponseDto(
            match.Id,
            opponentName,
            result,
            gameMode,
            playedAt, 
            duration
        );
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
using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Application.Interfaces;

public interface IMatchRepository
{
    Task<Match?> GetByIdAsync(Guid id);

    Task SaveAsync(Match match);

    // Para persistência de perfil/ranking
    Task UpdateUserProfileAsync(PlayerProfile profile);

    Task<PlayerProfile> GetUserProfileAsync(Guid userId);

    Task<Guid?> GetActiveMatchIdAsync(Guid userId);
}
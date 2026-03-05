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

    // Retorna IDs de todas as partidas contra IA que estão em andamento (para o background service de timeout)
    Task<List<Guid>> GetActiveAiMatchIdsAsync();

    Task UpdateAsync(Match match);

    Task DeleteAsync(Match match);
}
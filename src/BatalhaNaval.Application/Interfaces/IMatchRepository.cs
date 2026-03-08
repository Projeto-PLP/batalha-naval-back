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

    /// <summary>
    /// Retorna todas as partidas finalizadas em que o jogador participou, ordenadas da mais recente para a mais antiga.
    /// </summary>
    Task<List<Match>> GetPlayerMatchHistoryAsync(Guid playerId);
}
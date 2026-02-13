using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Interfaces;

public interface IMatchStateRepository
{
    /// <summary>
    ///     Salva o estado atual da partida no cache (Redis).
    /// </summary>
    Task SaveStateAsync(Match match);

    /// <summary>
    ///     Recupera a partida do cache e reconstrói a entidade de domínio.
    ///     Retorna null se não encontrar ou expirar.
    /// </summary>
    Task<Match?> GetStateAsync(Guid matchId);

    /// <summary>
    ///     Remove a partida do cache (usado ao finalizar o jogo).
    /// </summary>
    Task DeleteStateAsync(Guid matchId);

    /// <summary>
    ///     Verifica se existe uma partida ativa no cache.
    /// </summary>
    Task<bool> ExistsAsync(Guid matchId);
}
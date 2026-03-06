using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Application.Interfaces;

public interface ICampaignRepository
{
    /// <summary>Retorna o progresso de campanha do jogador, criando um registro inicial se não existir.</summary>
    Task<CampaignProgress> GetOrCreateProgressAsync(Guid userId);

    /// <summary>Retorna o progresso de campanha do jogador ou null se não existir.</summary>
    Task<CampaignProgress?> GetProgressAsync(Guid userId);

    /// <summary>Persiste alterações no progresso de campanha.</summary>
    Task UpdateProgressAsync(CampaignProgress progress);
}


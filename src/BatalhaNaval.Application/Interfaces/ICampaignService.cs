using BatalhaNaval.Application.DTOs;

namespace BatalhaNaval.Application.Interfaces;

public interface ICampaignService
{
    /// <summary>
    ///     Retorna o progresso atual da campanha do jogador.
    ///     Cria um progresso inicial (Stage1Basic) se o jogador ainda não iniciou a campanha.
    /// </summary>
    Task<CampaignProgressDto> GetProgressAsync(Guid userId);

    /// <summary>
    ///     Inicia uma partida do estágio atual da campanha do jogador.
    ///     Lança exceção se a campanha já foi concluída ou se o jogador já tem uma partida ativa.
    /// </summary>
    Task<StartCampaignMatchResponseDto> StartCampaignMatchAsync(Guid userId);
}


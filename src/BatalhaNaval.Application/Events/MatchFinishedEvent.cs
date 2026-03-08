using BatalhaNaval.Domain.Enums;
using MediatR;

namespace BatalhaNaval.Application.Events;

/// <summary>
///     Evento publicado pelo <see cref="BatalhaNaval.Application.Services.MatchService" /> quando uma partida termina.
///     Permite que outros domínios (ex: Campanha) reajam ao fim de uma partida
///     sem criar acoplamento direto com o serviço de partidas.
/// </summary>
public record MatchFinishedEvent(
    Guid MatchId,
    Guid Player1Id,
    Guid? WinnerId,
    bool IsCampaignMatch,
    CampaignStage? CampaignStage
) : INotification;



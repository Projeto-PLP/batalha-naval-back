using BatalhaNaval.Application.Events;
using BatalhaNaval.Application.Interfaces;
using MediatR;

namespace BatalhaNaval.Application.Handlers;

/// <summary>
///     Reage ao evento <see cref="MatchFinishedEvent" /> e avança o estágio da campanha
///     quando o jogador humano venceu uma partida de campanha.
///     O <see cref="BatalhaNaval.Application.Services.MatchService" /> não precisa conhecer
///     nada sobre o domínio de Campanha — apenas publica o evento.
/// </summary>
public class CampaignMatchFinishedHandler : INotificationHandler<MatchFinishedEvent>
{
    private readonly ICampaignRepository _campaignRepository;

    public CampaignMatchFinishedHandler(ICampaignRepository campaignRepository)
    {
        _campaignRepository = campaignRepository;
    }

    public async Task Handle(MatchFinishedEvent notification, CancellationToken cancellationToken)
    {
        // Ignora partidas fora do modo campanha
        if (!notification.IsCampaignMatch) return;

        // ── Matriz de resultados possíveis ───────────────────────────────────
        // VITÓRIA  : WinnerId == Player1Id          → avança estágio  ✔
        // DERROTA  : WinnerId != null && != Player1Id  → early-return   ✗
        // ABANDONO : WinnerId == null (AI match sem vencedor declarado) → early-return ✗
        // INATIVIDADE: WinnerId == Guid.Empty (sentinel) → early-return ✗
        // ─────────────────────────────────────────────────────────────────────

        // Sem vencedor declarado (abandono antes de fim de jogo / encerrado pelo servidor)
        if (!notification.WinnerId.HasValue) return;

        // Guid.Empty é o sentinel da IA — nunca representa um vencedor humano
        if (notification.WinnerId.Value == Guid.Empty) return;

        // O vencedor não é o jogador humano (perdeu para a IA ou oponente cancela
        // e o Player1 ganha, mas essa segunda hipótese avança corretamente)
        if (notification.WinnerId.Value != notification.Player1Id) return;

        var progress = await _campaignRepository.GetOrCreateProgressAsync(notification.Player1Id);

        // Idempotência: só avança se o estágio da partida bate com o estágio atual salvo.
        // Protege contra eventos duplicados ou partidas antigas reprocessadas.
        if (notification.CampaignStage != progress.CurrentStage) return;

        progress.AdvanceStage();
        await _campaignRepository.UpdateProgressAsync(progress);
    }
}



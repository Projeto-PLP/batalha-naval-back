using BatalhaNaval.Application.Interfaces;

namespace BatalhaNaval.API.BackgroundServices;

/// <summary>
///     Background Service que roda a cada 5 segundos e verifica todas as partidas
///     ativas contra IA. Se o jogador demorou mais de 31s para jogar, o turno é
///     trocado automaticamente e a IA joga imediatamente — sem depender de nenhuma
///     ação do jogador ou do frontend.
///     Delega toda a lógica de domínio e persistência ao MatchService.
/// </summary>
public class AiTimeoutBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private readonly ILogger<AiTimeoutBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AiTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AiTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AiTimeoutBackgroundService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            try
            {
                await ProcessTimeoutsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar timeouts de partidas contra IA.");
            }
        }

        _logger.LogInformation("AiTimeoutBackgroundService encerrado.");
    }

    private async Task ProcessTimeoutsAsync()
    {
        using var scope = _scopeFactory.CreateScope();

        var matchRepository = scope.ServiceProvider.GetRequiredService<IMatchRepository>();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchService>();

        // 1. Busca no SQL todos os IDs de partidas contra IA em andamento
        var activeAiMatchIds = await matchRepository.GetActiveAiMatchIdsAsync();

        foreach (var matchId in activeAiMatchIds)
        {
            try
            {
                // 2. Delega ao MatchService, que já possui toda a lógica de:
                //    - Aplicar timeout (ApplyTimeoutIfExpired)
                //    - Encerrar por inatividade (4 timeouts → ProcessEndGameAsync → ranking)
                //    - Acionar a IA (ProcessAiTurnLoopAsync)
                //    - Persistir no Redis e SQL corretamente
                var result = await matchService.CheckTurnTimeoutAsync(matchId);

                if (result.TurnSwitched)
                {
                    if (result.IsGameOver)
                        _logger.LogInformation(
                            "Partida {MatchId} encerrada por inatividade (4 timeouts consecutivos).", matchId);
                    else
                        _logger.LogInformation(
                            "Timeout detectado na partida {MatchId}. Turno trocado.", matchId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar timeout da partida {MatchId}.", matchId);
            }
        }
    }
}

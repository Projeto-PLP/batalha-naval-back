using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Domain.Exceptions;
using BatalhaNaval.Domain.Interfaces;

namespace BatalhaNaval.Application.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IUserRepository _userRepository;

    public CampaignService(
        ICampaignRepository campaignRepository,
        IMatchRepository matchRepository,
        IUserRepository userRepository)
    {
        _campaignRepository = campaignRepository;
        _matchRepository = matchRepository;
        _userRepository = userRepository;
    }

    // ====================================================================
    // CONSULTA DE PROGRESSO
    // ====================================================================

    public async Task<CampaignProgressDto> GetProgressAsync(Guid userId)
    {
        var progress = await _campaignRepository.GetOrCreateProgressAsync(userId);
        return MapToDto(progress);
    }

    // ====================================================================
    // INICIAR PARTIDA DE CAMPANHA
    // ====================================================================

    public async Task<StartCampaignMatchResponseDto> StartCampaignMatchAsync(Guid userId)
    {
        var playerExists = await _userRepository.ExistsAsync(userId);
        if (!playerExists)
            throw new KeyNotFoundException($"O Jogador com ID '{userId}' não foi encontrado.");

        var progress = await _campaignRepository.GetOrCreateProgressAsync(userId);

        // --- GUARDA 1: Campanha já concluída ---
        if (progress.IsCompleted)
            throw new CampaignAlreadyCompletedException();

        // --- GUARDA 2: Partida ativa em curso ---
        var activeMatchId = await _matchRepository.GetActiveMatchIdAsync(userId);
        if (activeMatchId.HasValue)
            throw new UserHasActiveMatchException(activeMatchId.Value);

        // --- FONTE DA VERDADE: O backend define hardcoded GameMode e Difficulty por estágio.
        //     O cliente nunca envia esses parâmetros — qualquer tentativa de manipulação
        //     via HTTP é ignorada porque o serviço não aceita esses dados de entrada. ---
        var (gameMode, difficulty) = progress.CurrentStage switch
        {
            CampaignStage.Stage1Basic        => (GameMode.Classic, Difficulty.Basic),
            CampaignStage.Stage2Intermediate => (GameMode.Classic, Difficulty.Intermediate),
            CampaignStage.Stage3Advanced     => (GameMode.Classic, Difficulty.Advanced),

            // Caso Completed já foi barrado acima; este branch é só para segurança do compilador.
            _ => throw new InvalidOperationException(
                     $"Estágio de campanha inesperado: '{progress.CurrentStage}'. Contate o suporte.")
        };

        var match = new Match(userId, gameMode, difficulty)
        {
            IsCampaignMatch = true,
            CampaignStage   = progress.CurrentStage
        };

        await _matchRepository.SaveAsync(match);

        return new StartCampaignMatchResponseDto(
            match.Id,
            progress.CurrentStage,
            difficulty,
            $"Partida de campanha iniciada! Estágio: {StageDescription(progress.CurrentStage)}. Boa sorte!"
        );
    }


    // ====================================================================
    // HELPERS PRIVADOS
    // ====================================================================

    private static CampaignProgressDto MapToDto(CampaignProgress progress)
    {
        return new CampaignProgressDto(
            progress.CurrentStage,
            StageDescription(progress.CurrentStage),
            progress.IsCompleted,
            progress.CurrentStageDifficulty,
            StageObjective(progress.CurrentStage),
            progress.CompletedAt,
            progress.UpdatedAt
        );
    }

    private static string StageDescription(CampaignStage stage) => stage switch
    {
        CampaignStage.Stage1Basic        => "Estágio 1 — IA Básica",
        CampaignStage.Stage2Intermediate => "Estágio 2 — IA Intermediária",
        CampaignStage.Stage3Advanced     => "Estágio 3 — IA Avançada",
        CampaignStage.Completed          => "Campanha Concluída",
        _                                => "Desconhecido"
    };

    private static string? StageObjective(CampaignStage stage) => stage switch
    {
        CampaignStage.Stage1Basic        => "Vença a IA Básica para desbloquear o Estágio 2.",
        CampaignStage.Stage2Intermediate => "Vença a IA Intermediária para desbloquear o Estágio 3.",
        CampaignStage.Stage3Advanced     => "Vença a IA Avançada para concluir a campanha.",
        CampaignStage.Completed          => null,
        _                                => null
    };
}




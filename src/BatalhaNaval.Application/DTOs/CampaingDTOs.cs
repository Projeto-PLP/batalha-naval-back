using BatalhaNaval.Domain.Enums;

namespace BatalhaNaval.Application.DTOs;

// === DTOs de SAÍDA ===

/// <summary>Estado atual da campanha do jogador.</summary>
/// <param name="CurrentStageDifficulty">Null quando a campanha já foi concluída (IsCompleted = true).</param>
/// <param name="CurrentStageObjective">Desafio/objetivo do estágio atual. Null quando IsCompleted = true.</param>
public record CampaignProgressDto(
    CampaignStage CurrentStage,
    string CurrentStageName,
    bool IsCompleted,
    Difficulty? CurrentStageDifficulty,
    string? CurrentStageObjective,
    DateTime? CompletedAt,
    DateTime UpdatedAt
);

/// <summary>Resposta ao iniciar uma partida de campanha.</summary>
/// <param name="AiDifficulty">Dificuldade da IA para este estágio.</param>
public record StartCampaignMatchResponseDto(
    Guid MatchId,
    CampaignStage Stage,
    Difficulty? AiDifficulty,
    string Message
);
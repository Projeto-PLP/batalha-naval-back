namespace BatalhaNaval.Application.DTOs;

/// <summary>
/// Representa uma entrada no histórico de partidas finalizadas de um jogador.
/// </summary>
public record MatchHistoryResponseDto(
    Guid Id,
    string? OpponentName,
    string Result,
    string GameMode,
    DateTime PlayedAt,
    TimeSpan? Duration
);


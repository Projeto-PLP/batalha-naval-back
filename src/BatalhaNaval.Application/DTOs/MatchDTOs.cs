using System.ComponentModel.DataAnnotations;
using BatalhaNaval.Domain.Enums;

// <--- IMPORTANTE: Adicione este using

namespace BatalhaNaval.Application.DTOs;

// === DTOs de ENTRADA (Inputs) ===

// Entrada para criar partida
public record StartMatchInput(
    GameMode Mode,
    Difficulty? AiDifficulty = null,
    Guid? OpponentId = null
);

// Mudamos de sintaxe posicional para sintaxe de propriedades
public record ShootInput
{
    [Required(ErrorMessage = "O ID da partida é obrigatório.")]
    public Guid MatchId { get; init; }

    [Required(ErrorMessage = "A coordenada X é obrigatória.")]
    [Range(0, 9, ErrorMessage = "A coordenada X deve estar entre 0 e 9.")]
    public int? X { get; init; } // int? (nullable) obriga o envio do valor

    [Required(ErrorMessage = "A coordenada Y é obrigatória.")]
    [Range(0, 9, ErrorMessage = "A coordenada Y deve estar entre 0 e 9.")]
    public int? Y { get; init; } // int? (nullable) obriga o envio do valor
}

// Para evitar envio de movimento vazio
public record MoveShipInput
{
    [Required] public Guid MatchId { get; init; }

    [Required] public Guid ShipId { get; init; }

    [Required(ErrorMessage = "A direção do movimento é obrigatória.")]
    public MoveDirection? Direction { get; init; } // Enum nullable
}

// Entrada para posicionar navios (Setup)
public record PlaceShipsInput(Guid MatchId, List<ShipPlacementDto> Ships);

public record ShipPlacementDto(string Name, int Size, int StartX, int StartY, ShipOrientation Orientation);

// === DTOs de SAÍDA (Outputs / View Models) ===

// Retorno simplificado para ações de jogo (Tiro/Movimento)
public record TurnResultDto(
    bool IsHit,
    bool IsSunk,
    bool IsGameOver,
    Guid? WinnerId,
    string Message
);

// Retorno COMPLETO do Estado da Partida (com Fog of War)
public record MatchGameStateDto(
    Guid MatchId,
    MatchStatus Status,
    Guid CurrentTurnPlayerId,
    bool IsMyTurn, // Facilita pro Frontend saber se habilita os controles
    Guid? WinnerId,
    BoardStateDto MyBoard, // Tabuleiro do jogador (vê tudo)
    BoardStateDto OpponentBoard, // Tabuleiro do oponente (mascarado)
    MatchStatsDto Stats
);

public record BoardStateDto(
    List<List<CellState>> Grid, // A matriz visual 10x10
    List<ShipDto>? Ships // Lista de navios (Null ou vazia para o oponente)
);

public record ShipDto(
    Guid Id,
    string Name,
    int Size,
    bool IsSunk,
    ShipOrientation Orientation,
    List<CoordinateDto> Coordinates
);

public record CoordinateDto(int X, int Y, bool IsHit);

public record MatchStatsDto(
    int MyHits,
    int MyStreak,
    int MyMisses,
    int OpponentHits,
    int OpponentStreak,
    int OpponentMisses
);
using System.Text.Json.Serialization;
using BatalhaNaval.Domain.Enums;

namespace BatalhaNaval.Domain.Entities;

public class MatchRedis
{
    [JsonPropertyName("MatchId")] public string MatchId { get; set; }

    [JsonPropertyName("GameMode")] public GameModeRedis GameMode { get; set; }

    [JsonPropertyName("AiDifficulty")] public AiDifficultyRedis? AiDifficulty { get; set; }

    [JsonPropertyName("Player1Id")] public string Player1Id { get; set; }

    [JsonPropertyName("Player2Id")] public string? Player2Id { get; set; }

    [JsonPropertyName("Status")] public MatchStatusRedis Status { get; set; }

    [JsonPropertyName("TurnNumber")] public int TurnNumber { get; set; }

    [JsonPropertyName("TurnPlayerId")] public string TurnPlayerId { get; set; }

    [JsonPropertyName("P1_ConsecutiveTimeouts")]
    public int P1_ConsecutiveTimeouts { get; set; }

    [JsonPropertyName("P2_ConsecutiveTimeouts")]
    public int P2_ConsecutiveTimeouts { get; set; }

    [JsonPropertyName("TurnStartedAt")] public long TurnStartedAt { get; set; }

    [JsonPropertyName("P1_Stats")] public PlayerStatsRedis P1_Stats { get; set; } = new();

    [JsonPropertyName("P2_Stats")] public PlayerStatsRedis P2_Stats { get; set; } = new();

    [JsonPropertyName("Boards")] public MatchBoardsRedis Boards { get; set; } = new();
}

public class PlayerStatsRedis
{
    [JsonPropertyName("Streak")] public int Streak { get; set; }

    [JsonPropertyName("Hits")] public int Hits { get; set; }

    [JsonPropertyName("Misses")] public int Misses { get; set; }
}

public class MatchBoardsRedis
{
    [JsonPropertyName("P1")] public PlayerBoardRedis P1 { get; set; } = new();

    [JsonPropertyName("P2")] public PlayerBoardRedis P2 { get; set; } = new();
}

public class PlayerBoardRedis
{
    [JsonPropertyName("AliveShips")] public int AliveShips { get; set; }

    // Chave: "x,y"(Ex: "0,1")
    // Valor: 0 (Miss) ou 1 (Hit)
    // Dicionário para validação de colisão e/ou movimento
    [JsonPropertyName("OceanGrid")] public Dictionary<string, int> OceanGrid { get; set; } = new();

    [JsonPropertyName("Ships")] public List<ShipRedis> Ships { get; set; } = new();
}

public class ShipRedis
{
    [JsonPropertyName("Id")] public string Id { get; set; }

    [JsonPropertyName("Type")] public string Type { get; set; }

    [JsonPropertyName("Size")] public int Size { get; set; }

    [JsonPropertyName("Orientation")] public ShipOrientationRedis Orientation { get; set; }

    [JsonPropertyName("Sunk")] public bool Sunk { get; set; }

    [JsonPropertyName("IsDamaged")] public bool IsDamaged { get; set; }

    [JsonPropertyName("Segments")] public List<ShipSegmentRedis> Segments { get; set; } = new();
}

public class ShipSegmentRedis
{
    [JsonPropertyName("x")] public int X { get; set; }

    [JsonPropertyName("y")] public int Y { get; set; }

    [JsonPropertyName("hit")] public bool Hit { get; set; }
}
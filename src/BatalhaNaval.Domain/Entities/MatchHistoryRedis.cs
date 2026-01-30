using System.Text.Json.Serialization;
using BatalhaNaval.Domain.Enums;

namespace BatalhaNaval.Domain.Entities;

public class MatchHistoryRedis
{
    [JsonPropertyName("type")] public HistoryEventTypeRedis Type { get; set; }

    [JsonPropertyName("turn")] public int Turn { get; set; }

    [JsonPropertyName("player")] public string Player { get; set; }

    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }

    [JsonPropertyName("payload")] public HistoryPayloadRedis Payload { get; set; }
}

// SHOT e MOVE
public class HistoryPayloadRedis
{
    // SHOT

    [JsonPropertyName("coord")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Coord { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Result { get; set; }

    // MOVE

    [JsonPropertyName("shipIdx")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ShipIdx { get; set; }

    [JsonPropertyName("direction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Direction { get; set; }

    [JsonPropertyName("orientation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ShipOrientationRedis? Orientation { get; set; }

    [JsonPropertyName("from")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ShipSegmentRedis>? From { get; set; }

    [JsonPropertyName("to")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ShipSegmentRedis>? To { get; set; }
}
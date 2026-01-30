using System.Text.Json.Serialization;

namespace BatalhaNaval.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameModeRedis
{
    CLASSIC,
    DYNAMIC
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiDifficultyRedis
{
    BASIC,
    INTERMEDIATE,
    ADVANCED
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MatchStatusRedis
{
    SETUP,
    IN_PROGRESS,
    FINISHED
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipOrientationRedis
{
    HORIZONTAL,
    VERTICAL,

    // TODO ver se deixa NONE ou MIXED
    NONE,
    MIXED
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HistoryEventTypeRedis
{
    SHOT,
    MOVE,
    TIMEOUT
}
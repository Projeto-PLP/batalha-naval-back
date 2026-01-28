using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Rules.Medals;

// Contexto contendo todos os dados necessários para avaliar se ganhou a medalha
public class MedalContext
{
    public Match Match { get; set; }
    public PlayerProfile Profile { get; set; }

    public Guid PlayerId { get; set; }

    // Dados estatísticos calculados da partida atual
    public int MaxConsecutiveHitsInMatch { get; set; }
    public bool WonWithoutLosingShips { get; set; }
    public TimeSpan MatchDuration { get; set; }
}
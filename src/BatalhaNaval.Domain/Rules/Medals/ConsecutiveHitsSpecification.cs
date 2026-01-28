using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Rules.Medals;

public class ConsecutiveHitsSpecification : IMedalSpecification
{
    public bool IsSatisfiedBy(MedalContext context, Medal medalDefinition)
    {
        var targetStreak = GetTargetStreak(medalDefinition.Code);

        if (targetStreak == 0) return false;

        return context.MaxConsecutiveHitsInMatch >= targetStreak;
    }

    private int GetTargetStreak(string code)
    {
        // Adicionar código da medalha nova no banco e ajustar aqui.
        return code switch
        {
            "CAPTAIN" => 7,
            "CAPTAIN_WAR" => 8,
            _ => 0
        };
    }
}
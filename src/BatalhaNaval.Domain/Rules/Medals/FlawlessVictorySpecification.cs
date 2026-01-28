using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Rules.Medals;

public class FlawlessVictorySpecification : IMedalSpecification
{
    public bool IsSatisfiedBy(MedalContext context, Medal medalDefinition)
    {
        if (medalDefinition.Code != "ADMIRAL") return false;

        // Vencer sem perder navios
        return context.Match.WinnerId == context.PlayerId && context.WonWithoutLosingShips;
    }
}
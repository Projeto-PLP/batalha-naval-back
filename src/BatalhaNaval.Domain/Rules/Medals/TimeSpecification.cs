using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Rules.Medals;

public class TimeSpecification : IMedalSpecification
{
    public bool IsSatisfiedBy(MedalContext context, Medal medalDefinition)
    {
        //vai verificar aqui se a medalha e a de tempo determinado
        if (medalDefinition.Code != "SAILOR") return false;

        var targetTime = TimeSpan.FromMinutes(2);

        return context.Match.WinnerId == context.PlayerId && context.MatchDuration <= targetTime;
    }
}
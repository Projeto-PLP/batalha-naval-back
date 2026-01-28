using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Rules.Medals;

public interface IMedalSpecification
{
    // Define para qual Code esta regra serve
    bool IsSatisfiedBy(MedalContext context, Medal medalDefinition);
}
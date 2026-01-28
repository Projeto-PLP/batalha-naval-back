using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Interfaces;
using BatalhaNaval.Domain.Rules.Medals;

namespace BatalhaNaval.Application.Services;

public class MedalService
{
    private readonly IMedalRepository _medalRepository;
    private readonly IEnumerable<IMedalSpecification> _rules;

    public MedalService(IMedalRepository medalRepo, IEnumerable<IMedalSpecification> rules)
    {
        _medalRepository = medalRepo;
        _rules = rules;
    }

    public async Task<List<Medal>> CheckAndAwardMedalsAsync(MedalContext context)
    {
        var newMedals = new List<Medal>();

        var existingUserMedals = await _medalRepository.GetUserMedalsAsync(context.PlayerId);
        var existingMedalIds = existingUserMedals.Select(um => um.MedalId).ToHashSet();

        var allMedals = await _medalRepository.GetAllAsync();

        foreach (var medal in allMedals)
        {
            if (existingMedalIds.Contains(medal.Id)) continue;

            foreach (var rule in _rules)
                if (rule.IsSatisfiedBy(context, medal))
                {
                    newMedals.Add(medal);

                    var newAchievement = new UserMedal(context.PlayerId, medal.Id);

                    await _medalRepository.AddUserMedalAsync(newAchievement);

                    if (context.Profile != null) context.Profile.AddMedal(medal.Id);

                    break;
                }
        }

        return newMedals;
    }
}
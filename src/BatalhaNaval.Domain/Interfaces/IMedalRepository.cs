using BatalhaNaval.Domain.Entities;

namespace BatalhaNaval.Domain.Interfaces;

public interface IMedalRepository
{
    Task<IEnumerable<Medal>> GetAllAsync();

    Task<Medal?> GetByCodeAsync(string code);

    Task<IEnumerable<UserMedal>> GetUserMedalsAsync(Guid userId);

    Task AddUserMedalAsync(UserMedal userMedal);
}
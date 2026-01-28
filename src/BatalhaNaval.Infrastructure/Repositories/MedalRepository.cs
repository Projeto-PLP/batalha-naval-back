using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Interfaces;
using BatalhaNaval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BatalhaNaval.Infrastructure.Repositories;

public class MedalRepository : IMedalRepository
{
    private readonly BatalhaNavalDbContext _context;

    public MedalRepository(BatalhaNavalDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Medal>> GetAllAsync()
    {
        return await _context.Medals
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Medal?> GetByCodeAsync(string code)
    {
        return await _context.Medals
            .FirstOrDefaultAsync(m => m.Code == code);
    }

    public async Task<IEnumerable<UserMedal>> GetUserMedalsAsync(Guid userId)
    {
        return await _context.UserMedals
            .Include(um => um.Medal)
            .Where(um => um.UserId == userId)
            .ToListAsync();
    }

    public async Task AddUserMedalAsync(UserMedal userMedal)
    {
        await _context.UserMedals.AddAsync(userMedal);
        await _context.SaveChangesAsync();
    }
}